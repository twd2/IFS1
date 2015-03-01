Imports System.IO
Imports System.Text

Public Class IFS1DirBlock
    Inherits IFS1Block
    Implements IFS1BlockWithName
    Implements IFS1BlockWithTime
    Implements IFS1BlockWithIDs

    '//280
    'int32				used;						//1=使用, 0=未使用
    'int32				type;						//3
    'int8				dirname[256];				//目录名
    'struct CMOSDateTime	create;						//创建时间
    'struct CMOSDateTime	change;						//修改时间

    '//744
    'uint8				reserve[744];				//保留

    '//64512
    'uint32				blockids[16128];			//文件/目录块ID集合. 如果为0xFFFFFFFF, 则未指向任何文件/目录块.

    Private namedata(256 - 1) As Byte
    Private create As New CMOSDateTime, change As New CMOSDateTime

    'Public reserve(744 - 1) As Byte

    Private blockids(16128 - 1) As UInt32

    Public Sub New()
        type = BlockType.Dir
    End Sub

    Public Overloads Shared Function Read(s As Stream) As IFS1DirBlock
        Dim r As New IFS1DirBlock
        r.used = BinaryHelper.ReadInt32LE(s)
        r.type = BinaryHelper.ReadInt32LE(s)
        If r.type <> BlockType.Dir Then
            Throw New IFS1BadFileSystemException("Type mismatch!")
        End If
        BinaryHelper.SafeRead(s, r.namedata, 0, 256)
        r._name = BinaryHelper.GetString(r.namedata)
        r.create = CMOSDateTime.Read(s)
        r.change = CMOSDateTime.Read(s)
        s.Seek(744, SeekOrigin.Current) 'skip
        For i = 0 To 16128 - 1
            r.blockids(i) = BinaryHelper.ReadUInt32LE(s)
        Next
        Return r
    End Function

    Public Overrides Sub Write(s As Stream)
        BinaryHelper.WriteInt32LE(s, used)
        BinaryHelper.WriteInt32LE(s, type)
        s.Write(namedata, 0, 256)
        create.Write(s)
        change.Write(s)
        s.Seek(744, SeekOrigin.Current)
        For i = 0 To 16128 - 1
            BinaryHelper.WriteUInt32LE(s, blockids(i))
        Next
    End Sub

    Private _name As String
    Public Property Name As String Implements IFS1BlockWithName.Name
        Get
            Return _name
        End Get
        Set(value As String)
            _name = value
            namedata = BinaryHelper.GetBytes(value)
        End Set
    End Property

    Public Overrides Function ToString() As String
        Return String.Format("Dirname: {0}", Name)
    End Function

    Public Overrides Function Clone() As IFS1Block
        Throw New NotImplementedException()
    End Function


    Public Property CreationTime As Date Implements IFS1BlockWithTime.CreationTime
        Get
            Return create.ToDate()
        End Get
        Set(value As Date)
            create.SetDate(value)
        End Set
    End Property

    Public Property LastAccessTime As Date Implements IFS1BlockWithTime.LastAccessTime
        Get
            Return change.ToDate()
        End Get
        Set(value As Date)
            'change.SetDate(value)
        End Set
    End Property
    Public Property LastWriteTime As Date Implements IFS1BlockWithTime.LastWriteTime
        Get
            Return change.ToDate()
        End Get
        Set(value As Date)
            change.SetDate(value)
        End Set
    End Property

    Public Property SubBlockIDs() As UInteger() Implements IFS1BlockWithIDs.SubBlockIDs
        Get
            Return blockids
        End Get
        Set(value As UInteger())
            blockids = value
        End Set
    End Property

End Class
