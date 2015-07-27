Imports System.IO

Public Class IFS1FileStream
    Inherits Stream

    Private ifs As IFS1
    Private path As String
    Private _block As IFS1FileBlock

    Public Sub New(ifs As IFS1, path As String)
        MyBase.New()
        Me.ifs = ifs
        Me.path = path
        _block = ifs.GetBlockByPathStrict(path, IFS1Block.BlockType.File)
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
            Return Not ifs.Options.ReadOnlyMount
        End Get
    End Property

    Public Overrides Sub Flush()
        ifs.Sync()
    End Sub

    Public Overrides ReadOnly Property Length As Long
        Get
            Return _block.Length
        End Get
    End Property

    Public Overrides Property Position As Long

    Public Overrides Function Read(buffer() As Byte, offset As Integer, count As Integer) As Integer
        Dim ret = ifs.Read(_block, buffer, Position, offset, count)
        Position += ret
        Return ret
    End Function

    Public Overrides Function Seek(offset As Long, origin As SeekOrigin) As Long
        If origin = SeekOrigin.Begin Then
            Position = offset
        ElseIf origin = SeekOrigin.Current Then
            Position += offset
        ElseIf origin = SeekOrigin.End Then
            Position = Length + offset
        End If
        If Position >= Length Then
            ifs.Resize(_block, Position)
            _block.Length = Position
        End If
        Return True
    End Function

    Public Overrides Sub SetLength(value As Long)
        ifs.Resize(_block, value)
        _block.Length = value
    End Sub

    Public Overrides Sub Write(buffer() As Byte, offset As Integer, count As Integer)
        Dim ret = ifs.Write(_block, buffer, Position, offset, count)
        Position += ret
    End Sub
End Class
