Imports System.IO

Public Class IFS1DataBlock
    Inherits IFS1Block
    Implements IFS1BlockWithLength

    '//12
    'int32	used;						//1=使用, 0=未使用
    'int32	type;						//2
    'uint32	length;						//长度

    '//500
    'uint8	reserve[500];				//保留

    '//65024
    'uint8	data[DATA_BLOCK_DATA_LEN];	//数据

    Private _length As UInt32

    'Public reserve(500 - 1) As Byte

    'offset=512

    Public data(-1) As Byte 'IFS1.DATA_BLOCK_DATA_LEN

    Public Sub New(Optional zerodata As Boolean = False)
        type = BlockType.Data
        If zerodata Then
            ReDim data(IFS1.DATA_BLOCK_DATA_LEN - 1)
        End If
    End Sub

    Public Overloads Shared Function Read(s As Stream) As IFS1DataBlock
        Dim r As New IFS1DataBlock(True)
        r.used = BinaryHelper.ReadInt32LE(s)
        r.type = BinaryHelper.ReadInt32LE(s)
        If r.type <> BlockType.Data Then
            Throw New IFS1BadFileSystemException("Type mismatch!")
        End If
        r._length = BinaryHelper.ReadUInt32LE(s)
        s.Seek(500, SeekOrigin.Current) 'skip
        BinaryHelper.SafeRead(s, r.data, 0, IFS1.DATA_BLOCK_DATA_LEN)
        Return r
    End Function

    Public Overloads Shared Function ReadWithoutData(s As Stream) As IFS1DataBlock
        Dim r As New IFS1DataBlock(False)
        r.used = BinaryHelper.ReadInt32LE(s)
        r.type = BinaryHelper.ReadInt32LE(s)
        If r.type <> BlockType.Data Then
            Throw New IFS1BadFileSystemException("Type mismatch!")
        End If
        r._length = BinaryHelper.ReadUInt32LE(s)
        s.Seek(500 + IFS1.DATA_BLOCK_DATA_LEN, SeekOrigin.Current) 'skip
        ReDim r.data(-1)
        'BinaryHelper.SafeRead(s, r.data, 0, IFS1.DATA_BLOCK_DATA_LEN)
        Return r
    End Function

    Public Overrides Sub Write(s As Stream)
        BinaryHelper.WriteInt32LE(s, used)
        BinaryHelper.WriteInt32LE(s, type)
        BinaryHelper.WriteUInt32LE(s, _length)
        s.Seek(500, SeekOrigin.Current)
        If data.Length > 0 Then
            s.Write(data, 0, data.Length)
            s.Seek(IFS1.DATA_BLOCK_DATA_LEN - data.Length, SeekOrigin.Current)
        Else
            s.Seek(IFS1.DATA_BLOCK_DATA_LEN, SeekOrigin.Current)
        End If
    End Sub

    Public Overrides Function Clone() As IFS1Block
        Dim r As New IFS1DataBlock(False)
        r.id = id
        r.used = used
        r.type = type
        r._length = _length
        ReDim r.data(data.Length - 1)
        Array.Copy(data, r.data, data.Length)
        Return r
    End Function

    Public Overrides Function CloneWithoutData() As IFS1Block
        Dim r As New IFS1DataBlock(False)
        r.used = used
        r.type = type
        r._length = _length
        ReDim r.data(-1)
        Return r
    End Function

    Public Property Length As UInteger Implements IFS1BlockWithLength.Length
        Get
            Return _length
        End Get
        Set(value As UInteger)
            _length = value
        End Set
    End Property

End Class
