﻿Imports System.IO

Partial Public Class IFS1
    Implements IDisposable

    Private _s As Stream

    Private RootBlock As IFS1DirBlock

    Private BlocksCache As New List(Of IFS1Block)

    Private pendingBlocks As New Queue(Of IFS1Block)

    Private CurrentTransaction As IFS1Transaction = Nothing

    Public ReadOnlyMount As Boolean = False
    Public AutoSync As Boolean = False

    Public Const DATA_BLOCK_DATA_LEN = 65024
    Public Const BLOCK_LEN = 64 * 1024
    Public Const FIRST_BLOCK_ID = 10
    Public Const INVALID_BLOCK_ID = CUInt(&HFFFFFFFFL)
    Public Const MAX_DIR_LEVEL = 4096

    ''' <summary>
    ''' 是否需要同步到磁盘
    ''' </summary>
    ''' <value></value>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public ReadOnly Property NeedSync As Boolean
        Get
            Return pendingBlocks.Count > 0
        End Get
    End Property

    Public Sub New(s As Stream, Optional check As Boolean = False, Optional repair As Boolean = False)
        Me._s = s

        Dim sw As New Stopwatch

        Console.WriteLine("Loading Blocks cache")
        sw.Start()
        LoadBlocksCache()
        RootBlock = BlocksCache(FIRST_BLOCK_ID)
        sw.Stop()
        Console.WriteLine("Load Blocks cache done {0}ms", sw.ElapsedMilliseconds)

        If check Then
            Console.WriteLine("Checking FS")
            sw.Restart()
            CheckFS(repair)
            sw.Stop()
            Console.WriteLine("Check FS done {0}ms", sw.ElapsedMilliseconds)
        End If

        'Try
        '    CreateSoftLink("/root", "/")
        'Catch ex As Exception
        '    Console.WriteLine(ex.ToString())
        'End Try
        'Try
        '    CreateSoftLink("/isystem/hello", "/isystem/")
        'Catch ex As Exception
        '    Console.WriteLine(ex.ToString())
        'End Try
        'Try
        '    CreateSoftLink("/isystem/hello.sl.txt", "/hello.txt")
        'Catch ex As Exception
        '    Console.WriteLine(ex.ToString())
        'End Try
        'Try
        '    CreateSoftLink("/tolink.txt", "/isystem/hello.sl.txt")
        'Catch ex As Exception
        '    Console.WriteLine(ex.ToString())
        'End Try
        'Try
        '    CreateSoftLink("/tolink2.txt", "/tolink.txt")
        'Catch ex As Exception
        '    Console.WriteLine(ex.ToString())
        'End Try
        'Try
        '    CreateSoftLink("/tolink", "/root")
        'Catch ex As Exception
        '    Console.WriteLine(ex.ToString())
        'End Try
        'Try
        '    CreateSoftLink("/tolink2", "/tolink")
        'Catch ex As Exception
        '    Console.WriteLine(ex.ToString())
        'End Try
        'Sync()
    End Sub

    ''' <summary>
    ''' 载入/刷新Block缓存
    ''' </summary>
    ''' <remarks></remarks>
    Public Sub LoadBlocksCache()
        Dim sec0 = ReadSector()
        If sec0(510) = &H55 AndAlso sec0(511) = &HAA Then '第0扇区
            _s.Seek(10 * BLOCK_LEN, SeekOrigin.Begin)
        End If
        For i = 0 To FIRST_BLOCK_ID - 1
            BlocksCache.Add(New IFS1Block With {.id = i, .used = &H7FFFFFFF})
        Next
        Dim blk = ReadBlock(False)
        Dim blkid = FIRST_BLOCK_ID
        Do While blk IsNot Nothing
            blk.id = blkid
            BlocksCache.Add(blk)
            blk = ReadBlock(False)
            blkid += 1
        Loop
    End Sub

    ''' <summary>
    ''' 入队Block修改
    ''' </summary>
    ''' <param name="newblock"></param>
    ''' <remarks></remarks>
    Public Sub EnqueueBlockChange(newblock As IFS1Block)
        If ReadOnlyMount Then
            Throw New IFS1NoPermissionException()
        End If
        SyncLock Me
            If CurrentTransaction IsNot Nothing Then
                CurrentTransaction.AddBlockChange(newblock)
            Else
                pendingBlocks.Enqueue(newblock)
                BlocksCache(newblock.id) = newblock
                If newblock.type = IFS1Block.BlockType.Data Then
                    Dim datablk = DirectCast(newblock.Clone(), IFS1DataBlock)
                    ReDim datablk.data(-1)
                    BlocksCache(newblock.id) = datablk
                End If
            End If
        End SyncLock

        If AutoSync Then
            Sync()
        End If
    End Sub

    ''' <summary>
    ''' 开启新事务
    ''' </summary>
    ''' <param name="name"></param>
    ''' <remarks></remarks>
    Public Sub NewTransaction(name As String)
        If CurrentTransaction IsNot Nothing Then
            CurrentTransaction.Rollback()
            'CommitTransaction()
        End If
        CurrentTransaction = New IFS1Transaction(name, Me)
    End Sub

    ''' <summary>
    ''' 递交事务
    ''' </summary>
    ''' <remarks></remarks>
    Public Sub CommitTransaction()
        Dim trans = CurrentTransaction
        CurrentTransaction = Nothing
        trans.Commit()
    End Sub

    ''' <summary>
    ''' 将Block修改写入磁盘
    ''' </summary>
    ''' <remarks></remarks>
    Public Sub Sync()
        SyncLock Me
            Do While pendingBlocks.Count > 0
                Dim blk = pendingBlocks.Dequeue()
                WriteBlock(blk)
            Loop
            _s.Flush()
        End SyncLock
    End Sub

    ''' <summary>
    ''' 获取子Block
    ''' </summary>
    ''' <param name="blk">父block</param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function GetSubBlocks(blk As IFS1BlockWithIDs) As List(Of IFS1Block)
        Dim r As New List(Of IFS1Block)
        For i = 0 To blk.SubBlockIDs.Length - 1
            If blk.SubBlockIDs(i) = INVALID_BLOCK_ID Then
                Exit For
            End If
            r.Add(BlocksCache(blk.SubBlockIDs(i)))
        Next
        Return r
    End Function

    ''' <summary>
    ''' 检查BlockID是否合法
    ''' </summary>
    ''' <param name="id"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function CheckID(id As UInt32) As Boolean
        Return id <> INVALID_BLOCK_ID
    End Function

    Public Function CountTotalBlocks() As UInt32
        Return BlocksCache.Count - FIRST_BLOCK_ID
    End Function

    Public Function CountUsedBlocks() As UInt32
        Dim usedblk = BlocksCache.FindAll(Function(blk As IFS1Block)
                                              Return blk.id >= FIRST_BLOCK_ID AndAlso blk.used <> 0
                                          End Function)
        Return usedblk.Count
    End Function

    Public Function GetBlockByPath(path As String) As IFS1Block
         Return GetBlockByPathStrict(path, IFS1Block.BlockType.Raw)
    End Function

    Public Function GetBlockByPathStrict(path As String, Optional nosoftlink As Boolean = False, Optional deep As Long = 0) As IFS1Block
        path = path.Replace("\"c, "/"c)
        Dim type = IFS1Block.BlockType.Dir
        If path.Last <> "/"c Then
            type = IFS1Block.BlockType.File
        End If
        Dim patharray = path.Split({"/"c}, StringSplitOptions.RemoveEmptyEntries)
        Return GetBlockByPathStrict(patharray, type, nosoftlink, deep)
    End Function

    Public Function GetBlockByPathStrict(path As String, type As IFS1Block.BlockType, Optional nosoftlink As Boolean = False, Optional deep As Long = 0) As IFS1Block
        path = path.Replace("\"c, "/"c)
        Dim patharray = path.Split({"/"c}, StringSplitOptions.RemoveEmptyEntries)
        Return GetBlockByPathStrict(patharray, type, nosoftlink, deep)
    End Function

    ''' <summary>
    ''' 根据路径查找相应的Block
    ''' </summary>
    ''' <param name="pathArray">路径</param>
    ''' <param name="type">期望获得的Block的类型</param>
    ''' <param name="noSoftLink">如果路径指向符号连接，是否重定向到被指向的Block，为True则不重定向</param>
    ''' <param name="deep">当前调用深度</param>
    ''' <returns>Block</returns>
    ''' <remarks></remarks>
    Public Function GetBlockByPathStrict(pathArray As String(), type As IFS1Block.BlockType, Optional noSoftLink As Boolean = False, Optional deep As Long = 0) As IFS1Block
        If pathArray.Length >= MAX_DIR_LEVEL Then
            Throw New IFS1PathTooLongException(pathArray)
        End If
        If deep >= MAX_DIR_LEVEL Then
            Throw New IFS1PathTooLongException(pathArray)
        End If

        '当前级目录
        Dim lastBlock As IFS1DirBlock = RootBlock
        For i = 0 To pathArray.Length - 1
            '子路径名
            Dim subPath = pathArray(i)

            Dim found As Boolean = False

            '寻找子Blocks中有名为subPath的Block
            For j = 0 To lastBlock.SubBlockIDs.Length - 1
                Dim subid = lastBlock.SubBlockIDs(j)
                If Not CheckID(subid) Then
                    Exit For
                End If
                Dim subblk = BlocksCache(subid)

                If (Not TypeOf subblk Is IFS1BlockWithName) OrElse DirectCast(subblk, IFS1BlockWithName).Name <> subPath Then
                    Continue For
                End If

                If i = pathArray.Length - 1 Then
                    '最后一个了，允许为文件或者符号连接
                    If subblk.type = type OrElse type = IFS1Block.BlockType.Raw Then
                        '找到了符合type要求的block
                        '如果是Raw的话，所有block都符合要求
                        Return subblk
                    ElseIf Not noSoftLink AndAlso
                        subblk.type = IFS1Block.BlockType.SoftLink AndAlso
                        (type = IFS1Block.BlockType.Dir OrElse type = IFS1Block.BlockType.File) Then
                        '如果开启了符号连接并且找到了一个符号连接
                        Dim softlinkblk = DirectCast(subblk, IFS1SoftLinkBlock)
                        Dim toblk = GetBlockByPathStrict(softlinkblk.To, type, , deep + 1)
                        If toblk.type = type Then
                            Return toblk
                        Else
                            Exit For
                        End If
                    End If
                Else
                    If subblk.type = IFS1Block.BlockType.SoftLink OrElse
                       subblk.type = IFS1Block.BlockType.Dir Then
                        found = True

                        '如果是符号链接的话就要使用被指向的block
                        If subblk.type = IFS1Block.BlockType.SoftLink Then
                            subblk = GetBlockByPathStrict(DirectCast(subblk, IFS1SoftLinkBlock).To, IFS1Block.BlockType.Dir, , deep + 1)
                        End If

                        lastBlock = subblk
                        Exit For
                    End If
                End If
            Next
            If Not found Then
                Throw New IFS1PathNotFoundException(pathArray)
            End If
        Next
        Return lastBlock
    End Function

    Public Function Read(path As String, buffer As Byte(), fileoffset As UInt32, bufferoffset As UInt32, count As UInt32) As UInt32
        Dim blk As IFS1FileBlock = GetBlockByPathStrict(path, IFS1Block.BlockType.File)

        If fileoffset >= blk.Length Then
            Return 0
        End If

        Dim readlength = 0
        Do While readlength < count
            Dim currentoffset = readlength + fileoffset
            If currentoffset >= blk.Length Then
                Return readlength
            End If
            'Dim indexofid = CUInt(Math.Floor(currentoffset / IFS1.DATA_BLOCK_DATA_LEN))
            'Dim offsetofblock = currentoffset Mod IFS1.DATA_BLOCK_DATA_LEN

            Dim offsetofblock As Long
            Dim indexofid = Math.DivRem(currentoffset, IFS1.DATA_BLOCK_DATA_LEN, offsetofblock)

            Dim datablk As IFS1DataBlock = ReadBlockByID(blk.SubBlockIDs(indexofid))
            If datablk Is Nothing Then
                Return readlength
            End If

            Dim currentread = Math.Min(Math.Min(Math.Min(IFS1.DATA_BLOCK_DATA_LEN - offsetofblock,
                                count - readlength),
                                 blk.Length - currentoffset), datablk.Length - offsetofblock)

            If currentread > 0 Then
                Array.Copy(datablk.data, offsetofblock, buffer, readlength + bufferoffset, currentread)
            Else
                Return readlength
            End If

            readlength += currentread
        Loop
        Return readlength
    End Function

    Private Sub Compress(blk As IFS1FileBlock, newsize As UInt32)
        If ReadOnlyMount Then
            Throw New IFS1NoPermissionException()
        End If
        If newsize = blk.Length Then
            Return
        End If
        Dim oldsize = blk.Length
        If newsize > blk.Length Then
            Throw New ArgumentException("newsize")
        End If
        NewTransaction("Compress " + blk.Name)

        Dim oldblockcount = CUInt(Math.Ceiling(oldsize / IFS1.DATA_BLOCK_DATA_LEN)),
         newblockcount = CUInt(Math.Ceiling(newsize / IFS1.DATA_BLOCK_DATA_LEN))
        Dim deltacount = oldblockcount - newblockcount

        If deltacount = 0 Then '不需要删除块
            If newsize <= 0 Then
                Return
            End If
            Dim newoffsetofblock = (newsize - 1) Mod IFS1.DATA_BLOCK_DATA_LEN
            If oldblockcount > 0 Then
                Dim lastdatablk As IFS1DataBlock = BlocksCache(blk.SubBlockIDs(oldblockcount - 1))
                lastdatablk.Length = newoffsetofblock + 1
                EnqueueBlockChange(lastdatablk)
            End If
        Else
            If oldblockcount > 0 Then
                Dim lastdatablk As IFS1DataBlock = BlocksCache(blk.SubBlockIDs(oldblockcount - 1))
                lastdatablk.Length = IFS1.DATA_BLOCK_DATA_LEN
                EnqueueBlockChange(lastdatablk)
            End If
            Dim usedid As New List(Of UInt32)
            For i = newblockcount - 1 + 1 To oldblockcount - 1
                Dim datablk As IFS1DataBlock = BlocksCache(blk.SubBlockIDs(i))
                datablk.used = 0
                EnqueueBlockChange(datablk)

                blk.SubBlockIDs(i) = INVALID_BLOCK_ID
            Next
        End If

        blk.Length = newsize
        EnqueueBlockChange(blk)

        CommitTransaction()
    End Sub

    Private Sub Expand(blk As IFS1FileBlock, newsize As UInt32, Optional zerodata As Boolean = False)
        If ReadOnlyMount Then
            Throw New IFS1NoPermissionException()
        End If
        If newsize = blk.Length Then
            Return
        End If
        Dim oldsize = blk.Length
        If newsize < blk.Length Then
            Throw New ArgumentException("newsize")
        End If
        NewTransaction("Expand " + blk.Name)

        Dim oldblockcount = CUInt(Math.Ceiling(oldsize / IFS1.DATA_BLOCK_DATA_LEN)),
         newblockcount = CUInt(Math.Ceiling(newsize / IFS1.DATA_BLOCK_DATA_LEN))
        Dim deltacount = newblockcount - oldblockcount

        If deltacount = 0 Then '不需要新分配块
            If newsize <= 0 Then
                Return
            End If
            Dim newoffsetofblock = (newsize - 1) Mod IFS1.DATA_BLOCK_DATA_LEN
            If oldblockcount > 0 Then
                Dim lastdatablk As IFS1DataBlock = BlocksCache(blk.SubBlockIDs(oldblockcount - 1))
                lastdatablk.Length = newoffsetofblock + 1
                EnqueueBlockChange(lastdatablk)
            End If
        Else
            If oldblockcount > 0 Then
                Dim lastdatablk As IFS1DataBlock = BlocksCache(blk.SubBlockIDs(oldblockcount - 1))
                lastdatablk.Length = IFS1.DATA_BLOCK_DATA_LEN
                EnqueueBlockChange(lastdatablk)
            End If
            'Dim usedid As New List(Of UInt32)
            Dim newblkids = AllocBlock(deltacount)
            For i = oldblockcount - 1 + 1 To newblockcount - 1
                Dim newblkid = newblkids(i - (oldblockcount - 1 + 1))

                Dim datablk As New IFS1DataBlock(zerodata)
                datablk.id = newblkid
                If i < newblockcount - 1 Then
                    datablk.Length = IFS1.DATA_BLOCK_DATA_LEN
                Else
                    Dim newoffsetofblock = (newsize - 1) Mod IFS1.DATA_BLOCK_DATA_LEN
                    datablk.Length = newoffsetofblock + 1
                End If

                'ReDim datablk.data(-1)
                datablk.used = 1

                blk.SubBlockIDs(i) = newblkid

                EnqueueBlockChange(datablk)
            Next
        End If
        blk.Length = newsize
        EnqueueBlockChange(blk)

        CommitTransaction()
    End Sub

    Public Sub Resize(blk As IFS1FileBlock, newsize As UInt32)
        If ReadOnlyMount Then
            Throw New IFS1NoPermissionException()
        End If
        If newsize > blk.Length Then
            Expand(blk, newsize)
        ElseIf newsize < blk.Length Then
            Compress(blk, newsize)
        End If
    End Sub

    Public Function Write(path As String, buffer As Byte(), fileoffset As UInt32, bufferoffset As UInt32, count As UInt32) As UInt32
        If ReadOnlyMount Then
            Throw New IFS1NoPermissionException()
        End If
        Dim blk As IFS1FileBlock = GetBlockByPathStrict(path, IFS1Block.BlockType.File)

        If fileoffset + buffer.Length > blk.Length Then
            Expand(blk, fileoffset + buffer.Length)
            Sync()
        End If

        blk.LastWriteTime = DateTime.Now
        EnqueueBlockChange(blk)

        Dim writtenlength = 0
        Do While writtenlength < count
            Dim currentoffset = writtenlength + fileoffset
            Debug.Assert(currentoffset < blk.Length)

            Dim offsetofblock As Long
            Dim indexofid = Math.DivRem(currentoffset, IFS1.DATA_BLOCK_DATA_LEN, offsetofblock)

            Dim datablk As IFS1DataBlock
            ''这里需要先从缓存中读取Block, 否则上次写入的Block会丢失
            'datablk = BlocksCache(blk.blockids(indexofid))
            'If datablk.data.Length <= 0 Then
            datablk = ReadBlockByID(blk.SubBlockIDs(indexofid))
            'End If

            Debug.Assert(datablk IsNot Nothing)

            Dim currentwrite = Math.Min(Math.Min(Math.Min(IFS1.DATA_BLOCK_DATA_LEN - offsetofblock,
                                count - writtenlength),
                                 blk.Length - currentoffset), datablk.Length - offsetofblock)

            If currentwrite > 0 Then
                Array.Copy(buffer, writtenlength + bufferoffset, datablk.data, offsetofblock, currentwrite)
                EnqueueBlockChange(datablk)
            Else
                Return writtenlength
            End If

            writtenlength += currentwrite
        Loop

        Sync()
        Return writtenlength
    End Function

    Private Sub Unlink(parent As IFS1Block, blkid As UInt32)
        If ReadOnlyMount Then
            Throw New IFS1NoPermissionException()
        End If

        If TypeOf parent Is IFS1BlockWithIDs Then
            Dim blkwithids = DirectCast(parent, IFS1BlockWithIDs)
            Dim index = -1
            For i = 0 To blkwithids.SubBlockIDs.Length - 1
                If blkwithids.SubBlockIDs(i) = blkid Then
                    blkwithids.SubBlockIDs(i) = INVALID_BLOCK_ID
                    index = i
                    Exit For
                End If
            Next
            If index >= 0 Then
                For i = blkwithids.SubBlockIDs.Length - 1 To index + 1 Step -1
                    If CheckID(blkwithids.SubBlockIDs(i)) Then
                        blkwithids.SubBlockIDs(index) = blkwithids.SubBlockIDs(i)
                        blkwithids.SubBlockIDs(i) = INVALID_BLOCK_ID
                        Exit For
                    End If
                Next
            End If
            If TypeOf parent Is IFS1BlockWithTime Then
                Dim blkwithtime = DirectCast(parent, IFS1BlockWithTime)
                blkwithtime.LastWriteTime = DateTime.Now
            End If
            EnqueueBlockChange(parent)
        Else
            Throw New ArgumentException("parent")
        End If
    End Sub

    Private Sub Link(parent As IFS1Block, blkid As UInt32)
        If ReadOnlyMount Then
            Throw New IFS1NoPermissionException()
        End If

        If TypeOf parent Is IFS1BlockWithIDs Then
            Dim blkwithids = DirectCast(parent, IFS1BlockWithIDs)

            Dim succ As Boolean = False
            For i = 0 To blkwithids.SubBlockIDs.Length - 1
                If Not CheckID(blkwithids.SubBlockIDs(i)) Then
                    blkwithids.SubBlockIDs(i) = blkid
                    succ = True
                    Exit For
                End If
            Next
            If Not succ Then
                Throw New IFS1AllocationFailedException("Cannot link block to parent!")
            End If

            If TypeOf parent Is IFS1BlockWithTime Then
                Dim blkwithtime = DirectCast(parent, IFS1BlockWithTime)
                blkwithtime.LastWriteTime = DateTime.Now
            End If
            EnqueueBlockChange(parent)
        Else
            Throw New ArgumentException("parent")
        End If
    End Sub

    Public Function PathExists(path As String) As Boolean
        Try
            Dim blk = GetBlockByPath(path)
            Return True
        Catch ex As Exception
            Return False
        End Try
    End Function

    Public Function DirExists(path As String) As Boolean
        Try
            Dim blk = GetBlockByPathStrict(path, IFS1Block.BlockType.Dir)
            Return True
        Catch ex As Exception
            Return False
        End Try
    End Function

    Public Function FileExists(path As String) As Boolean
        Try
            Dim blk = GetBlockByPathStrict(path, IFS1Block.BlockType.File)
            Return True
        Catch ex As IFS1PathNotFoundException
            Return False
        End Try
    End Function

    Private Function SplitPath(path As String, ByRef outChlid As String) As String()
        path = path.Replace("\"c, "/"c)
        Dim patharray = path.Split({"/"c}, StringSplitOptions.RemoveEmptyEntries)
        outChlid = patharray.Last
        ReDim Preserve patharray(patharray.Length - 2)
        Return patharray
    End Function

    Public Sub CreateDir(path As String)
        If ReadOnlyMount Then
            Throw New IFS1NoPermissionException()
        End If
        If DirExists(path) Then
            Throw New IFS1DirAlreadyExistsException(path)
        End If
        Dim newdirname = Nothing
        Dim patharray = SplitPath(path, newdirname)
        Dim parent As IFS1DirBlock = GetBlockByPathStrict(patharray, IFS1Block.BlockType.Dir)
        Dim newblockid = AllocBlock()
        If Not CheckID(newblockid) Then
            Throw New IFS1AllocationFailedException()
        End If
        Dim newblock As New IFS1DirBlock()
        newblock.id = newblockid
        newblock.used = 1
        For i = 0 To newblock.SubBlockIDs.Length - 1
            newblock.SubBlockIDs(i) = INVALID_BLOCK_ID
        Next
        newblock.Name = newdirname
        Link(parent, newblockid)
        EnqueueBlockChange(newblock)
    End Sub

    Public Sub Move(oldPath As String, newPath As String)
        If ReadOnlyMount Then
            Throw New IFS1NoPermissionException()
        End If
        Dim oldName = Nothing
        Dim oldPathArray = SplitPath(oldPath, oldName)
        Dim oldParent As IFS1DirBlock = GetBlockByPathStrict(oldPathArray, IFS1Block.BlockType.Dir)

        Dim newName = Nothing
        Dim newPathArray = SplitPath(newPath, newName)
        Dim newParent As IFS1DirBlock = GetBlockByPathStrict(newPathArray, IFS1Block.BlockType.Dir)

        Dim blk = GetBlockByPath(oldPath)

        If TypeOf blk Is IFS1BlockWithName Then
            Dim blkwithname = DirectCast(blk, IFS1BlockWithName)
            If blkwithname.Name <> newName Then
                blkwithname.Name = newName
                EnqueueBlockChange(blkwithname)
            End If
        End If

        If TypeOf blk Is IFS1BlockWithTime Then
            Dim blkwithtime = DirectCast(blk, IFS1BlockWithTime)
            blkwithtime.LastWriteTime = DateTime.Now
            EnqueueBlockChange(blkwithtime)
        End If

        Unlink(oldParent, blk.id)
        Link(newParent, blk.id)
    End Sub

    Public Sub CreateEmptyFile(path As String)
        If ReadOnlyMount Then
            Throw New IFS1NoPermissionException()
        End If
        If FileExists(path) Then
            Throw New IFS1FileAlreadyExistsException(path)
        End If
        Dim newfilename = Nothing
        Dim patharray = SplitPath(path, newfilename)
        Dim parent As IFS1DirBlock = GetBlockByPathStrict(patharray, IFS1Block.BlockType.Dir)
        Dim newblockid = AllocBlock()
        If Not CheckID(newblockid) Then
            Throw New IFS1AllocationFailedException()
        End If
        Dim newblock As New IFS1FileBlock()
        newblock.id = newblockid
        newblock.used = 1
        newblock.Length = 0
        For i = 0 To newblock.SubBlockIDs.Length - 1
            newblock.SubBlockIDs(i) = INVALID_BLOCK_ID
        Next
        newblock.Name = newfilename
        Link(parent, newblockid)
        EnqueueBlockChange(newblock)
    End Sub

    Public Sub CreateSoftLink(path As String, topath As String)
        'Throw New NotImplementedException("CreateSoftLink")
        If ReadOnlyMount Then
            Throw New IFS1NoPermissionException()
        End If
        If PathExists(path) Then
            Throw New IFS1PathAlreadyExistsException(path)
        End If
        If Not PathExists(topath) Then
            Throw New IFS1PathNotFoundException(topath)
        End If

        NewTransaction("CreateSoftLink")

        Dim newfilename = Nothing
        Dim patharray = SplitPath(path, newfilename)
        Dim parent As IFS1DirBlock = GetBlockByPathStrict(patharray, IFS1Block.BlockType.Dir)
        Dim newblockid = AllocBlock()
        If Not CheckID(newblockid) Then
            Throw New IFS1AllocationFailedException()
        End If

        Dim newblock As New IFS1SoftLinkBlock()
        newblock.id = newblockid
        newblock.used = 1
        newblock.Name = newfilename
        newblock.To = topath

        Link(parent, newblockid)
        EnqueueBlockChange(newblock)

        CommitTransaction()
    End Sub

    Public Sub DeleteSoftLink(path As String)
        'Throw New NotImplementedException("DeleteSoftLink")
        If ReadOnlyMount Then
            Throw New IFS1NoPermissionException()
        End If
        Dim filename = Nothing
        Dim patharray = SplitPath(path, filename)
        Dim parent As IFS1DirBlock = GetBlockByPathStrict(patharray, IFS1Block.BlockType.Dir)
        Dim blk As IFS1SoftLinkBlock = GetBlockByPathStrict(path, IFS1Block.BlockType.SoftLink, True)
        blk.used = 0

        Unlink(parent, blk.id)

        EnqueueBlockChange(blk)
    End Sub

    Public Sub DeleteDir(path As String)
        If ReadOnlyMount Then
            Throw New IFS1NoPermissionException()
        End If
        path = path.Replace("\"c, "/"c)
        Dim patharray = path.Split({"/"c}, StringSplitOptions.RemoveEmptyEntries)
        If patharray.Length = 0 Then
            Throw New IFS1NoPermissionException("Cannot delete /")
        End If
        Dim dirname = patharray.Last
        ReDim Preserve patharray(patharray.Length - 2)
        Dim parent As IFS1DirBlock = GetBlockByPathStrict(patharray, IFS1Block.BlockType.Dir)

        Dim blk As IFS1DirBlock = GetBlockByPathStrict(path, IFS1Block.BlockType.Dir, True)
        If CheckID(blk.SubBlockIDs(0)) Then
            ForceDeleteDirBlockRecursive(blk)
        End If
        blk.used = 0

        Unlink(parent, blk.id)

        EnqueueBlockChange(blk)
    End Sub

    ''' <summary>
    ''' 使用需谨慎, 请务必先解除和别的Block的联系
    ''' </summary>
    ''' <param name="blk"></param>
    ''' <remarks></remarks>
    Public Sub ForceDeleteDirBlockRecursive(blk As IFS1DirBlock)
        If ReadOnlyMount Then
            Throw New IFS1NoPermissionException()
        End If
        For i = 0 To blk.SubBlockIDs.Length - 1
            If Not CheckID(blk.SubBlockIDs(i)) Then
                Exit For
            End If
            Dim subblk = BlocksCache(blk.SubBlockIDs(i))
            If subblk.type = IFS1Block.BlockType.Dir Then
                ForceDeleteDirBlockRecursive(subblk)
            ElseIf subblk.type = IFS1Block.BlockType.File Then
                ForceDeleteFileBlock(subblk)
            Else
                subblk.used = 0
                EnqueueBlockChange(subblk)
            End If
        Next

        blk.used = 0
        EnqueueBlockChange(blk)
    End Sub

    ''' <summary>
    ''' 使用需谨慎, 请务必先解除和别的Block的联系
    ''' </summary>
    ''' <param name="blk"></param>
    ''' <remarks></remarks>
    Private Sub ForceDeleteFileBlock(blk As IFS1FileBlock)
        If ReadOnlyMount Then
            Throw New IFS1NoPermissionException()
        End If
        For i = 0 To blk.SubBlockIDs.Length - 1
            If Not CheckID(blk.SubBlockIDs(i)) Then
                Exit For
            End If
            Dim subblk = BlocksCache(blk.SubBlockIDs(i))
            subblk.used = 0
            EnqueueBlockChange(subblk)
            blk.SubBlockIDs(i) = INVALID_BLOCK_ID
        Next

        blk.used = 0
        EnqueueBlockChange(blk)
    End Sub

    Public Sub DeleteFile(path As String)
        If ReadOnlyMount Then
            Throw New IFS1NoPermissionException()
        End If
        path = path.Replace("\"c, "/"c)
        Dim patharray = path.Split({"/"c}, StringSplitOptions.RemoveEmptyEntries)
        Dim filename = patharray.Last
        ReDim Preserve patharray(patharray.Length - 2)
        Dim parent As IFS1DirBlock = GetBlockByPathStrict(patharray, IFS1Block.BlockType.Dir)
        Dim blk As IFS1FileBlock = GetBlockByPathStrict(path, IFS1Block.BlockType.File, True)

        ForceDeleteFileBlock(blk)

        Unlink(parent, blk.id)

        EnqueueBlockChange(blk)
    End Sub

    Public Sub WriteBlock(blk As IFS1Block)
        If ReadOnlyMount Then
            Throw New IFS1NoPermissionException()
        End If

        Using ms As New MemoryStream(BLOCK_LEN)
            blk.Write(ms)
            SeekBlock(blk.id, SeekOrigin.Begin)
            'blk.Write(_s) 
            _s.Write(ms.ToArray(), 0, ms.Length)
        End Using

    End Sub

    Public Function ReadBlockByID(id As UInt32, Optional readdata As Boolean = True) As IFS1Block
        If Not CheckID(id) Then
            Return Nothing
        End If
        SeekBlock(id, SeekOrigin.Begin)
        Dim blk = ReadBlock(readdata)
        blk.id = id
        Return blk
    End Function

    Public Sub SeekBlock(id As UInt32, so As SeekOrigin)
        _s.Seek(id * BLOCK_LEN, so)
    End Sub

    Public Function ReadBlock(Optional readdata As Boolean = True) As IFS1Block
        If _s.Length - _s.Position < BLOCK_LEN Then
            Return Nothing
        End If
        _s.Seek(4, SeekOrigin.Current) 'skip used
        Dim type = BinaryHelper.ReadInt32LE(_s)
        _s.Seek(-8, SeekOrigin.Current) 'back
        Select Case type
            Case IFS1Block.BlockType.Raw
                If readdata Then
                    Return IFS1Block.Read(_s)
                Else
                    Return IFS1Block.ReadWithoutData(_s)
                End If
            Case IFS1Block.BlockType.File
                Return IFS1FileBlock.Read(_s)
            Case IFS1Block.BlockType.Data
                If readdata Then
                    Return IFS1DataBlock.Read(_s)
                Else
                    Return IFS1DataBlock.ReadWithoutData(_s)
                End If
            Case IFS1Block.BlockType.Dir
                Return IFS1DirBlock.Read(_s)
            Case IFS1Block.BlockType.SoftLink
                Return IFS1SoftLinkBlock.Read(_s)
            Case Else
                Debug.Print("Warning: Unknown block! Reading as unused RawBlock")
                Dim blk As IFS1Block
                If readdata Then
                    blk = IFS1Block.Read(_s)
                Else
                    blk = IFS1Block.ReadWithoutData(_s)
                End If
                blk.used = 0
                blk.type = IFS1Block.BlockType.Raw
                Return blk
        End Select
    End Function

    Public Sub SetTime(path As String, ctime As Date?, atime As Date?, mtime As Date?)
        Dim blk = GetBlockByPath(path)
        If TypeOf blk Is IFS1BlockWithTime Then
            Dim blkwithtime = DirectCast(blk, IFS1BlockWithTime)
            If ctime IsNot Nothing Then
                blkwithtime.CreationTime = ctime
            End If
            If atime IsNot Nothing Then
                blkwithtime.LastAccessTime = atime
            End If
            If mtime IsNot Nothing Then
                blkwithtime.LastWriteTime = mtime
            End If
            EnqueueBlockChange(blk)
        Else
            Throw New IFS1PathNotFoundException(path)
        End If
    End Sub

    Public Function IsSoftLink(path As String) As Boolean
        Dim blk = GetBlockByPathStrict(path, IFS1Block.BlockType.Raw)
        Return TypeOf blk Is IFS1SoftLinkBlock 
    End Function

    Public Function CheckFS(repair As Boolean) As Boolean
        If repair AndAlso ReadOnlyMount Then
            Throw New IFS1NoPermissionException()
        End If

        NewTransaction("Repair FS")

        '检查RootBlock
        If RootBlock.type <> IFS1Block.BlockType.Dir Then
            Console.WriteLine("RootBlock.Type != Dir, check failed!")
            Throw New IFS1BadFileSystemException("Type mismatch!")
        End If
        If RootBlock.Name <> "/" Then
            Console.WriteLine("RootBlock.Name != ""/""!")
            RootBlock.Name = "/"
            EnqueueBlockChange(RootBlock)
        End If

        '检查是否有未被引用的标记为已使用的块
        Dim refed(BlocksCache.Count - 1) As Boolean
        For i = FIRST_BLOCK_ID To BlocksCache.Count - 1
            refed(i) = False
        Next
        For i = FIRST_BLOCK_ID To BlocksCache.Count - 1
            Dim blk = BlocksCache(i)

            If blk.used = 0 Then
                Continue For
            End If

            If TypeOf blk Is IFS1BlockWithIDs Then
                Dim blkwithids = DirectCast(blk, IFS1BlockWithIDs)
                For j = 0 To blkwithids.SubBlockIDs.Length - 1
                    If Not CheckID(blkwithids.SubBlockIDs(j)) Then
                        Exit For
                    End If
                    refed(blkwithids.SubBlockIDs(j)) = True
                Next
            End If

            If TypeOf blk Is IFS1BlockWithName Then
                Dim blkwithname = DirectCast(blk, IFS1BlockWithName)
                If blkwithname.Name = "" Then
                    Console.WriteLine("Block " + i.ToString() + ".name=""""!")
                    If repair Then
                        blkwithname.Name = "block" + i.ToString()
                        EnqueueBlockChange(blkwithname)
                    End If
                End If
            End If

            If TypeOf blk Is IFS1BlockWithTime Then
                Dim blkwithtime = DirectCast(blk, IFS1BlockWithTime)
                If blkwithtime.CreationTime > DateTime.Now Then
                    Console.WriteLine("Block " + i.ToString() + ".CreationTime>Now!")
                End If
                If blkwithtime.LastAccessTime > DateTime.Now Then
                    Console.WriteLine("Block " + i.ToString() + ".LastAccessTime>Now!")
                End If
                If blkwithtime.LastWriteTime > DateTime.Now Then
                    Console.WriteLine("Block " + i.ToString() + ".LastWriteTime>Now!")
                End If

                Try
                    blkwithtime.CreationTime.ToFileTimeUtc()
                Catch ex As Exception
                    Console.WriteLine("Block " + i.ToString() + ".CreationTime wrong!")
                    If repair Then
                        blkwithtime.CreationTime = DateTime.Now
                        EnqueueBlockChange(blkwithtime)
                    End If
                End Try
                Try
                    blkwithtime.LastAccessTime.ToFileTimeUtc()
                Catch ex As Exception
                    Console.WriteLine("Block " + i.ToString() + ".LastAccessTime wrong!")
                    If repair Then
                        blkwithtime.LastAccessTime = DateTime.Now
                        EnqueueBlockChange(blkwithtime)
                    End If
                End Try
                Try
                    blkwithtime.LastWriteTime.ToFileTimeUtc()
                Catch ex As Exception
                    Console.WriteLine("Block " + i.ToString() + ".LastWriteTime wrong!")
                    If repair Then
                        blkwithtime.LastWriteTime = DateTime.Now
                        EnqueueBlockChange(blkwithtime)
                    End If
                End Try
            End If

            If TypeOf blk Is IFS1SoftLinkBlock Then
                Dim softlink = DirectCast(blk, IFS1SoftLinkBlock)
                If softlink.To Is Nothing Then
                    Console.WriteLine("CANNOT REPAIR: Block " + i.ToString() + ".To is nothing!")
                ElseIf Not PathExists(softlink.To) Then
                    Console.WriteLine("CANNOT REPAIR: Block " + i.ToString() + ".To Not Exists!")
                End If
            End If
        Next

        Dim flag = True
        For i = FIRST_BLOCK_ID + 1 To BlocksCache.Count - 1
            If BlocksCache(i).used <> 0 AndAlso Not refed(i) Then
                Console.WriteLine("Block " + i.ToString() + " not refed but used!")
                If repair Then
                    BlocksCache(i).used = 0
                    EnqueueBlockChange(BlocksCache(i))
                End If
                flag = False
            End If
        Next
        CommitTransaction()
        Sync()
        Return flag
    End Function

    Public Sub MakeFS()
        'TODO
    End Sub


#Region "IDisposable Support"
    Private disposedValue As Boolean ' 检测冗余的调用

    ' IDisposable
    Protected Overridable Sub Dispose(disposing As Boolean)
        If Not Me.disposedValue Then
            If disposing Then
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
    Public Sub Dispose() Implements IDisposable.Dispose
        ' 不要更改此代码。    请将清理代码放入上面的 Dispose (disposing As Boolean)中。
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub
#End Region

End Class
