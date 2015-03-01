Imports System.IO
Imports System.Text
Public Class IFS1SoftLinkBlock
    Inherits IFS1Block
    Implements IFS1BlockWithName

    '//264
    'int32				used;					//1=使用, 0=未使用
    'int32				type;					//1
    'int8				filename[256];			//文件名

    '//248
    'uint8 				reserve[248];			//保留

    '//65024
    'int8				to[65024];			//指向何方

    Private namedata(256 - 1) As Byte

    Private todata(65024 - 1) As Byte

    Public Sub New()
        type = BlockType.SoftLink
    End Sub

    Public Overloads Shared Function Read(s As Stream) As IFS1SoftLinkBlock
        Dim r As New IFS1SoftLinkBlock
        r.used = BinaryHelper.ReadInt32LE(s)
        r.type = BinaryHelper.ReadInt32LE(s)
        If r.type <> BlockType.SoftLink Then
            Throw New IFS1BadFileSystemException("Type mismatch!")
        End If
        BinaryHelper.SafeRead(s, r.namedata, 0, 256)
        r._name = BinaryHelper.GetString(r.namedata)
        s.Seek(248, SeekOrigin.Current) 'skip
        BinaryHelper.SafeRead(s, r.todata, 0, 65024)
        r._to = BinaryHelper.GetString(r.todata)
        Return r
    End Function

    Public Overrides Sub Write(s As Stream)
        BinaryHelper.WriteInt32LE(s, used)
        BinaryHelper.WriteInt32LE(s, type)
        s.Write(namedata, 0, 256)
        s.Seek(248, SeekOrigin.Current)
        s.Write(todata, 0, todata.Length)
    End Sub

    Private _name As String
    Public Property Name() As String Implements IFS1BlockWithName.Name
        Get
            Return _name
        End Get
        Set(value As String)
            _name = value
            namedata = BinaryHelper.GetBytes(value)
        End Set
    End Property

    Private _to As String
    Public Property [To]() As String
        Get
            Return _to
        End Get
        Set(value As String)
            _to = value
            todata = BinaryHelper.GetBytes(value)
        End Set
    End Property

End Class
