﻿Imports System.IO

Class DeviceStream
    Inherits Stream
    Implements IDisposable

    Public Const SECTOR_LEN = 512 * 8 '* 128

    Private _dev As String
    Private hFile As Integer
    Private _mode As Integer
    Private _currentSector As ULong = 0

    Sub New(deviceName As String, mode As Integer)
        _mode = mode
        _dev = deviceName
        hFile = Win32Native.CreateFile("\\.\" + deviceName, _mode Or Win32Native.GENERIC_READ, 0, 0, Win32Native.OPEN_EXISTING, 0, 0)
        Win32Native.AssumeNoError()
    End Sub

    Public Overrides ReadOnly Property CanRead As Boolean
        Get
            Return True
        End Get
    End Property

    Public Overrides ReadOnly Property CanSeek As Boolean
        Get
            Return True
        End Get
    End Property

    Public Overrides ReadOnly Property CanWrite As Boolean
        Get
            Return _mode And Win32Native.GENERIC_WRITE
        End Get
    End Property

    Public Overrides Sub Flush()

    End Sub

    Private _length As Long? = Nothing
    Public Overrides Sub SetLength(len As Long)
        _length = len
    End Sub

    Public Overrides ReadOnly Property Length As Long
        Get
            If _length IsNot Nothing Then
                Return _length
            Else
                Dim size = 0
                Win32Native.GetFileSize(hFile, size)
                Win32Native.AssumeNoError()
                _length = size
                Return size
            End If
        End Get
    End Property

    Public Overrides Property Position As Long = 0

    Public Overrides Function Read(buffer() As Byte, offset As Integer, count As Integer) As Integer
        If Position >= Length Then
            Return 0
        End If

        Dim readlength = 0
        Do While readlength < count
            Dim currentoffset = readlength + Position
            If currentoffset >= Length Then
                Position += readlength
                Return readlength
            End If

            Dim offsetOfSector As Long
            Dim indexOfSector = Math.DivRem(currentoffset, SECTOR_LEN, offsetOfSector)

            Dim sectorData(SECTOR_LEN - 1) As Byte
            Dim win32read = 0
            SeekToSector(indexOfSector)
            Dim code = Win32Native.ReadFile(hFile, sectorData(0), SECTOR_LEN, win32read, 0)
            If code <> 1 Then
                Win32Native.AssumeNoError()
            End If
            Debug.Assert(win32read = SECTOR_LEN)
            _currentSector += 1

            Dim currentread = Math.Min(Math.Min(SECTOR_LEN - offsetOfSector,
                                count - readlength),
                                 Length - currentoffset)

            If currentread > 0 Then
                Array.Copy(sectorData, offsetOfSector, buffer, readlength + offset, currentread)
            Else
                Position += readlength
                Return readlength
            End If

            readlength += currentread
        Loop
        Position += readlength
        Return readlength
    End Function

    Private Sub SeekToSector(n As Long)
        If _currentSector = n Then
            Return
        End If
        Dim tomove = n * SECTOR_LEN
        Win32Native.SetFilePointerEx(hFile, tomove, 0, 0)
        Win32Native.AssumeNoError()
        _currentSector = n
    End Sub

    Public Overrides Function Seek(offset As Long, origin As SeekOrigin) As Long
        If origin = SeekOrigin.Begin Then
            Position = offset
        ElseIf origin = SeekOrigin.Current Then
            Position += offset
        ElseIf origin = SeekOrigin.End Then
            Position = offset + Length
        End If
        Return Position
    End Function

    Public Overrides Sub Write(buffer() As Byte, offset As Integer, count As Integer)
        If Not CanWrite Then
            Throw New Exception("cannot write")
        End If

        If Position + buffer.Length > Length Then
            Throw New ArgumentException("out of range")
        End If


        Dim writtenlength = 0
        Do While writtenlength < count
            Dim currentoffset = writtenlength + Position
            'Debug.Assert(currentoffset < Length)

            Dim offsetOfSector As Long
            Dim indexOfSector = Math.DivRem(currentoffset, SECTOR_LEN, offsetOfSector)

            If offsetOfSector = 0 AndAlso buffer.Length - (writtenlength + offset) >= SECTOR_LEN Then
                'Dim sectorData(SECTOR_LEN - 1) As Byte
                'Array.Copy(buffer, writtenlength + offset, sectorData, 0, SECTOR_LEN)
                Dim win32wrote = 0
                SeekToSector(indexOfSector)
                'Win32Native.WriteFile(hFile, sectorData(0), SECTOR_LEN, win32wrote, 0)
                Dim code = Win32Native.WriteFile(hFile, buffer(writtenlength + offset), SECTOR_LEN, win32wrote, 0)
                If code <> 1 Then
                    Win32Native.AssumeNoError()
                End If
                Debug.Assert(win32wrote = SECTOR_LEN)
                _currentSector += 1
                writtenlength += SECTOR_LEN
            Else
                Dim sectorData(SECTOR_LEN - 1) As Byte
                Dim win32read = 0
                SeekToSector(indexOfSector)
                Dim code = Win32Native.ReadFile(hFile, sectorData(0), SECTOR_LEN, win32read, 0)
                If code <> 1 Then
                    Win32Native.AssumeNoError()
                End If
                Debug.Assert(win32read = SECTOR_LEN)
                _currentSector += 1

                Dim currentwrite = Math.Min(Math.Min(SECTOR_LEN - offsetOfSector,
                                    count - writtenlength),
                                     Length - currentoffset)

                If currentwrite > 0 Then
                    Array.Copy(buffer, writtenlength + offset, sectorData, offsetOfSector, currentwrite)
                    Dim win32wrote = 0
                    SeekToSector(indexOfSector)
                    code = Win32Native.WriteFile(hFile, sectorData(0), SECTOR_LEN, win32wrote, 0)
                    If code <> 1 Then
                        Win32Native.AssumeNoError()
                    End If
                    Debug.Assert(win32wrote = SECTOR_LEN)
                    _currentSector += 1
                Else
                    Position += writtenlength
                    Return 'writtenlength
                End If
                writtenlength += currentwrite
            End If
        Loop
        Position += writtenlength
        Return 'writtenlength
    End Sub

    Public Overrides Sub Close()
        Win32Native.CloseHandle(hFile)
        Try
            Win32Native.AssumeNoError()
        Catch ex As Exception

        End Try
    End Sub

#Region "IDisposable Support"
    Private disposedValue As Boolean ' 检测冗余的调用

    ' IDisposable
    Protected Overloads Sub Dispose(disposing As Boolean)
        If Not Me.disposedValue Then
            If disposing Then
                Close()
                ' TODO:  释放托管状态(托管对象)。
            End If

            ' TODO:  释放非托管资源(非托管对象)并重写下面的 Finalize()。
            ' TODO:  将大型字段设置为 null。
        End If
        Me.disposedValue = True
    End Sub

    ' TODO:  仅当上面的 Dispose(ByVal disposing As Boolean)具有释放非托管资源的代码时重写 Finalize()。
    'Protected Overrides Sub Finalize()
    '    ' 不要更改此代码。    请将清理代码放入上面的 Dispose(ByVal disposing As Boolean)中。
    '    Dispose(False)
    '    MyBase.Finalize()
    'End Sub

    ' Visual Basic 添加此代码是为了正确实现可处置模式。
    Public Overloads Sub Dispose() Implements IDisposable.Dispose
        ' 不要更改此代码。    请将清理代码放入上面的 Dispose (disposing As Boolean)中。
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub
#End Region

End Class
