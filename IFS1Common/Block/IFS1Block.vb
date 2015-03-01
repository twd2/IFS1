Imports System.IO

Public Class IFS1Block

    Enum BlockType
        Raw
        File
        Data
        Dir

        'New
        Meta = 1000
        SoftLink = 65536
        HardLink

        Invalid = CUInt(&H7FFFFFFF)
    End Enum

    Public id As UInt32 '文件中不存放
    Public Locked As Boolean '标记用，文件中不存放

    Public used As Int32
    Public type As BlockType = BlockType.Raw  'Int32
    Public rawdata(-1) As Byte '[65528]

    Public Shared Function Read(s As Stream) As IFS1Block
        Dim r As New IFS1Block
        r.used = BinaryHelper.ReadInt32LE(s)
        r.type = BinaryHelper.ReadInt32LE(s)
        ReDim r.rawdata(65528 - 1)
        BinaryHelper.SafeRead(s, r.rawdata, 0, 65528)
        Return r
    End Function

    Public Shared Function ReadWithoutData(s As Stream) As IFS1Block
        Dim r As New IFS1Block
        r.used = BinaryHelper.ReadInt32LE(s)
        r.type = BinaryHelper.ReadInt32LE(s)
        s.Seek(65528, SeekOrigin.Current)
        Return r
    End Function

    Public Overridable Sub Write(s As Stream)
        BinaryHelper.WriteInt32LE(s, used)
        BinaryHelper.WriteInt32LE(s, type)
        s.Write(rawdata, 0, 65528)
    End Sub

    Public Overridable Function Clone() As IFS1Block
        Dim r As New IFS1Block
        r.id = id
        r.used = used
        r.type = type
        ReDim r.rawdata(rawdata.Length - 1)
        Array.Copy(rawdata, r.rawdata, rawdata.Length)
        Return r
    End Function

    Public Overridable Function CloneWithoutData() As IFS1Block
        Dim r As New IFS1Block
        r.used = used
        r.type = type
        ReDim r.rawdata(-1)
        Return r
    End Function

End Class
