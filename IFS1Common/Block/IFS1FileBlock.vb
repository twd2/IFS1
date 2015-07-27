Imports System.IO
Imports System.Text
Imports System.Runtime.InteropServices

Public Class IFS1FileBlock
    Inherits IFS1Block
    Implements IFS1BlockWithName
    Implements IFS1BlockWithTime
    Implements IFS1BlockWithIDs
    Implements IFS1BlockWithLength

    Public Enum PropertyBits
        lock = 1 << 0
        userRead = 1 << 1
        userWrite = 1 << 2
        userExecute = 1 << 3
        groupRead = 1 << 4
        groupWrite = 1 << 5
        groupExecute = 1 << 6
        otherRead = 1 << 7
        otherWrite = 1 << 8
        otherExecute = 1 << 9
    End Enum

    '//288
    'int32				used;					//1=使用, 0=未使用
    'int32				type;					//1
    'uint32				length;					//长度
    'int8				filename[256];			//文件名
    'struct CMOSDateTime	create;					//创建时间
    'struct CMOSDateTime	change;					//修改时间
    'int32				lock;					//是否锁定

    '//736
    'uint8 				reserve[736];			//保留

    '//64512
    'uint32				blockids[16128];		//数据块ID集合. 如果为0xFFFFFFFF, 则未指向任何数据块.

    Private _length As UInt32

    Public Const NAME_BYTE_LENGTH = 256
    Private namedata(NAME_BYTE_LENGTH - 1) As Byte

    Private create As New CMOSDateTime, change As New CMOSDateTime
    Public propertydata As UInt32

    Public Const RESERVE_LENGTH = 736
    'Public reserve(736 - 1) As Byte

    Public Const SUB_BLOCK_COUNT = 16128

    Private blockids(SUB_BLOCK_COUNT - 1) As UInt32

    Public Sub New()
        type = BlockType.File
    End Sub

    Public Overloads Shared Function Read(s As Stream) As IFS1FileBlock
        Dim r As New IFS1FileBlock
        r.used = BinaryHelper.ReadInt32LE(s)
        r.type = BinaryHelper.ReadInt32LE(s)
        If r.type <> BlockType.File Then
            Throw New IFS1BadFileSystemException("Type mismatch!")
        End If
        r._length = BinaryHelper.ReadUInt32LE(s)
        BinaryHelper.SafeRead(s, r.namedata, 0, NAME_BYTE_LENGTH)
        r._name = BinaryHelper.GetString(r.namedata)
        r.create = CMOSDateTime.Read(s)
        r.change = CMOSDateTime.Read(s)
        r.propertydata = BinaryHelper.ReadUInt32LE(s)
        s.Seek(RESERVE_LENGTH, SeekOrigin.Current) 'skip

        Dim blockidsdata(SUB_BLOCK_COUNT * Marshal.SizeOf(GetType(UInt32)) - 1) As Byte
        BinaryHelper.SafeRead(s, blockidsdata, 0, blockidsdata.Length)
        r.blockids = BinaryHelper.ToArray(Of UInt32)(blockidsdata)
        'For i = 0 To SUB_BLOCK_COUNT - 1
        '    r.blockids(i) = BinaryHelper.ReadUInt32LE(s)
        'Next
        Return r
    End Function

    Public Overrides Sub Write(s As Stream, buffered As Boolean)
        BinaryHelper.WriteInt32LE(s, used, buffered)
        BinaryHelper.WriteInt32LE(s, type, buffered)
        BinaryHelper.WriteUInt32LE(s, _length, buffered)
        s.Write(namedata, 0, NAME_BYTE_LENGTH)
        create.Write(s)
        change.Write(s)
        BinaryHelper.WriteUInt32LE(s, propertydata, buffered)
        s.Seek(RESERVE_LENGTH, SeekOrigin.Current)

        Dim blockidsdata = BinaryHelper.ToBytes(Of UInt32)(blockids)
        s.Write(blockidsdata, 0, blockidsdata.Length)
        'For i = 0 To SUB_BLOCK_COUNT - 1
        '    BinaryHelper.WriteUInt32LE(s, blockids(i), buffered)
        'Next
    End Sub

    Private _name As String
    Public Property Name() As String Implements IFS1BlockWithName.Name
        Get
            Return _name
        End Get
        Set(value As String)
            _name = value
            namedata = BinaryHelper.GetBytes(value, NAME_BYTE_LENGTH)
        End Set
    End Property

    Public Overrides Function ToString() As String
        Return String.Format("Filename: {0}, {1}byte(s)", Name, _length)
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

    Public Property SubBlockIDs As UInteger() Implements IFS1BlockWithIDs.SubBlockIDs
        Get
            Return blockids
        End Get
        Set(value As UInteger())
            blockids = value
        End Set
    End Property

    Public Property Length As UInteger Implements IFS1BlockWithLength.Length
        Get
            Return _length
        End Get
        Set(value As UInteger)
            _length = value
        End Set
    End Property
End Class
