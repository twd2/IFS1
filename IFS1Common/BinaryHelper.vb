Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Text

Public Class BinaryHelper

    Public Shared Function ReadInt16LE(s As Stream) As Short
        Dim i As Short = 0
        i += CShort(s.ReadByte())
        i += CShort(s.ReadByte()) << 8
        Return i
    End Function

    Public Shared Sub WriteInt16LE(s As Stream, i As Short)
        s.WriteByte(i And &HFF)
        s.WriteByte(((i And &HFFFF) >> 8) And &HFF)
    End Sub

    Public Shared Function ReadInt64LE(s As Stream) As Long
        Dim data(7) As Byte
        If SafeRead(s, data, 0, 8) < 8 Then
            Throw New EndOfStreamException()
        End If

        Dim i As Long = 0
        i += CLng(data(0))
        i += CLng(data(1)) << 8
        i += CLng(data(2)) << 16
        i += CLng(data(3)) << 24
        i += CLng(data(4)) << 32
        i += CLng(data(5)) << 40
        i += CLng(data(6)) << 48
        i += CLng(data(7)) << 56
        Return i
    End Function

    Public Shared Sub WriteInt64LE(s As Stream, i As Long)
        Dim data(7) As Byte

        data(0) = (i And &HFF)
        data(1) = (((i And &HFFFFFFFFFFFFFFFF) >> 8) And &HFF)
        data(2) = (((i And &HFFFFFFFFFFFFFFFF) >> 16) And &HFF)
        data(3) = (((i And &HFFFFFFFFFFFFFFFF) >> 24) And &HFF)
        data(4) = (((i And &HFFFFFFFFFFFFFFFF) >> 32) And &HFF)
        data(5) = (((i And &HFFFFFFFFFFFFFFFF) >> 40) And &HFF)
        data(6) = (((i And &HFFFFFFFFFFFFFFFF) >> 48) And &HFF)
        data(7) = (((i And &HFFFFFFFFFFFFFFFF) >> 56) And &HFF)

        s.Write(Data, 0, 8)
    End Sub

    Public Shared Function ReadInt32LE(s As Stream) As Integer
        Dim data(3) As Byte
        If SafeRead(s, data, 0, 4) < 4 Then
            Throw New EndOfStreamException()
        End If

        Dim i As Integer = 0
        i += CInt(data(0))
        i += CInt(data(1)) << 8
        i += CInt(data(2)) << 16
        i += CInt(data(3)) << 24
        Return i
    End Function

    Public Shared Sub WriteInt32LE(s As Stream, i As Integer)
        Dim data(3) As Byte

        data(0) = (i And &HFF)
        data(1) = (((i And &HFFFFFFFF) >> 8) And &HFF)
        data(2) = (((i And &HFFFFFFFF) >> 16) And &HFF)
        data(3) = (((i And &HFFFFFFFF) >> 24) And &HFF)

        s.Write(data, 0, 4)
    End Sub

    Public Shared Function ReadUInt32LE(s As Stream) As UInteger
        Dim data(3) As Byte
        If SafeRead(s, data, 0, 4) < 4 Then
            Throw New EndOfStreamException()
        End If

        Dim i As UInteger = 0
        i += CUInt(data(0))
        i += CUInt(data(1)) << 8
        i += CUInt(data(2)) << 16
        i += CUInt(data(3)) << 24
        Return i
    End Function

    Public Shared Sub WriteUInt32LE(s As Stream, i As UInteger)
        Dim data(3) As Byte

        data(0) = (i And &HFF)
        data(1) = (((i And &HFFFFFFFF) >> 8) And &HFF)
        data(2) = (((i And &HFFFFFFFF) >> 16) And &HFF)
        data(3) = (((i And &HFFFFFFFF) >> 24) And &HFF)

        s.Write(data, 0, 4)
    End Sub

    Public Shared Sub WriteDoubleLE(s As Stream, i As Double)
        WriteInt64LE(s, DoubleToInt64(i))
    End Sub

    Public Shared Function ReadDoubleLE(s As Stream) As Double
        Return Int64ToDouble(ReadInt64LE(s))
    End Function

    Public Shared Function Int64ToDouble(i As Int64) As Double
        Dim float(0) As Double
        Marshal.Copy(Marshal.UnsafeAddrOfPinnedArrayElement({i}, 0), float, 0, 1)
        Return float(0)
    End Function

    Public Shared Function DoubleToInt64(f As Double) As Int64
        Dim int(0) As Int64
        Marshal.Copy(Marshal.UnsafeAddrOfPinnedArrayElement({f}, 0), int, 0, 1)
        Return int(0)
    End Function

    Public Const BLOCKLENGTH = 4096

    Public Shared Function SafeRead(s As Stream, buffer() As Byte, offset As Long, count As Long) As Long
        Dim readlength = 0
        Do While readlength < count
            Dim currentread = s.Read(buffer, offset + readlength, Math.Min(count - readlength, BLOCKLENGTH))
            If currentread = 0 Then
                Return readlength
            End If
            readlength += currentread
        Loop
        Return readlength
    End Function

    Public Shared Function GetString(b As Byte()) As String
        Dim sb As New StringBuilder
        For i = 0 To b.Length - 1
            If b(i) = 0 Then
                Exit For
            End If
            sb.Append(Chr(b(i)))
        Next
        Return sb.ToString()
    End Function

    Public Shared Function GetBytes(s As String) As Byte()
        If s.Length > 255 Then
            Throw New Exception("String too long")
        End If
        Dim data(255) As Byte
        data(255) = 0
        For i = 0 To s.Length - 1
            Try
                Dim code = Asc(s(i))
                If code <= 255 Then
                    data(i) = code
                Else
                    data(i) = Asc("x"c)
                End If
            Catch ex As OverflowException
                data(i) = Asc("x"c)
            End Try
        Next
        data(s.Length) = 0
        Return data
    End Function

    Public Shared Function SetBit(a As Int32, bit As Int32, value As Boolean) As Int32
        If value Then
            Return a Or bit
        Else
            Return a And (Not bit)
        End If
    End Function

    Public Shared Function GetBit(a As Int32, bit As Int32) As Boolean
        Return a Or bit
    End Function

    Public Shared Function SetBit(a As UInt32, bit As UInt32, value As Boolean) As UInt32
        If value Then
            Return a Or bit
        Else
            Return a And (Not bit)
        End If
    End Function

    Public Shared Function GetBit(a As UInt32, bit As UInt32) As Boolean
        Return a Or bit
    End Function

End Class
