Imports System.IO

Partial Public Class IFS1
    Implements IDisposable

    Private _s As Stream

    Private _rootBlock As IFS1DirBlock

    Private _blocksCache As New List(Of IFS1Block)

    Private _pendingBlocks As New Queue(Of IFS1Block)

    Private _currentTransaction As IFS1Transaction = Nothing

    Public Logger As LoggerWrapper

    Public Options As New IFS1MountOptions

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
            Return _pendingBlocks.Count > 0
        End Get
    End Property

    Public Sub New(logger As LoggerWrapper, s As Stream, opt As IFS1MountOptions)
        Me._s = s
        Me.Logger = logger
        Me.Options = opt

        Dim sw As New Stopwatch

        logger.WriteLine("Loading Blocks cache")
        sw.Start()
        LoadBlocksCache()
        _rootBlock = _blocksCache(FIRST_BLOCK_ID)
        sw.Stop()
        logger.WriteLine("Load Blocks cache done {0}ms", sw.ElapsedMilliseconds)

        If opt.Check Then
            logger.WriteLine("Checking FS")
            sw.Restart()
            Check(opt.Repair)
            sw.Stop()
            logger.WriteLine("Check FS done {0}ms", sw.ElapsedMilliseconds)
        End If
    End Sub

    ''' <summary>
    ''' 载入/刷新Block缓存
    ''' </summary>
    ''' <remarks></remarks>
    Public Sub LoadBlocksCache()
        Dim sec0 = ReadSector()
        If sec0(510) = &H55 AndAlso sec0(511) = &HAA Then '第0扇区
            _s.Seek(FIRST_BLOCK_ID * IFS1Block.BLOCK_LENGTH, SeekOrigin.Begin)
        End If
        For i = 0 To FIRST_BLOCK_ID - 1
            _blocksCache.Add(New IFS1Block With {.id = i, .used = &H7FFFFFFF})
        Next
        Dim blk = ReadBlock(False)
        Dim blkid = FIRST_BLOCK_ID
        Do While blk IsNot Nothing
            blk.id = blkid
            _blocksCache.Add(blk)
            blk = ReadBlock(False)
            blkid += 1
            If _s.Position Mod (233 * 512) = 0 Then
                Console.Write("{0}Loading Blocks {1}/{2}                      ", Chr(13), CLng(_s.Position / IFS1Block.BLOCK_LENGTH), CLng(_s.Length / IFS1Block.BLOCK_LENGTH))
            End If
        Loop
        Console.WriteLine()
    End Sub

    ''' <summary>
    ''' 刷新Block缓存
    ''' </summary>
    ''' <remarks></remarks>
    Public Sub ReloadBlocksCache()
        _s.Seek(0, SeekOrigin.Begin)
        LoadBlocksCache()
    End Sub

    ''' <summary>
    ''' 入队Block修改
    ''' </summary>
    ''' <param name="newblock"></param>
    ''' <remarks></remarks>
    Public Sub EnqueueBlockChange(newblock As IFS1Block)
        If Options.ReadOnlyMount Then
            Throw New IFS1NoPermissionException()
        End If
        SyncLock Me
            If _currentTransaction IsNot Nothing Then
                _currentTransaction.AddBlockChange(newblock)
            Else
                _pendingBlocks.Enqueue(newblock)
                _blocksCache(newblock.id) = newblock
                If newblock.type = IFS1Block.BlockType.Data Then
                    Dim datablk = DirectCast(newblock.Clone(), IFS1DataBlock)
                    ReDim datablk.data(-1)
                    _blocksCache(newblock.id) = datablk
                End If
            End If
        End SyncLock

        If Options.AutoSync Then
            Sync()
        End If
    End Sub

    ''' <summary>
    ''' 开启新事务
    ''' </summary>
    ''' <param name="name"></param>
    ''' <remarks></remarks>
    Public Sub NewTransaction(name As String)
        If _currentTransaction IsNot Nothing Then
            _currentTransaction.Rollback()
            'CommitTransaction()
        End If
        _currentTransaction = New IFS1Transaction(name, Me)
    End Sub

    ''' <summary>
    ''' 递交事务
    ''' </summary>
    ''' <remarks></remarks>
    Public Sub CommitTransaction()
        Dim trans = _currentTransaction
        _currentTransaction = Nothing
        trans.Commit()
    End Sub

    ''' <summary>
    ''' 将Block修改写入磁盘
    ''' </summary>
    ''' <remarks></remarks>
    Public Sub Sync()
        'Dim issync = NeedSync
        'If issync Then
        '    Logger.WriteLine("Syncing...")
        'End If

        SyncLock Me
            Do While _pendingBlocks.Count > 0
                Dim blk = _pendingBlocks.Dequeue()
                WriteBlock(blk)
            Loop
            _s.Flush()
        End SyncLock

        'If issync Then
        '    Logger.WriteLine("Sync done.")
        'End If
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
            r.Add(_blocksCache(blk.SubBlockIDs(i)))
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
        Return _blocksCache.Count - FIRST_BLOCK_ID
    End Function

    Public Function CountUsedBlocks() As UInt32
        Dim usedblk = _blocksCache.FindAll(Function(blk As IFS1Block)
                                               Return blk.id >= FIRST_BLOCK_ID AndAlso blk.used <> 0
                                           End Function)
        Return usedblk.Count
    End Function

    Public Function GetBlockByPath(path As String) As IFS1Block
        Return GetBlockByPathStrict(path, IFS1Block.BlockType.Raw)
    End Function

    Public Function GetBlockByPathStrict(path As String, Optional noSoftLink As Boolean = False, Optional deep As Long = 0) As IFS1Block
        path = path.Replace("\"c, "/"c)
        Dim type = IFS1Block.BlockType.Dir
        If path.Last <> "/"c Then
            type = IFS1Block.BlockType.File
        End If
        Dim patharray = path.Split({"/"c}, StringSplitOptions.RemoveEmptyEntries)
        Return GetBlockByPathStrict(patharray, type, noSoftLink, deep)
    End Function

    Public Function GetBlockByPathStrict(path As String, type As IFS1Block.BlockType, Optional noSoftLink As Boolean = False, Optional deep As Long = 0) As IFS1Block
        path = path.Replace("\"c, "/"c)
        Dim patharray = path.Split({"/"c}, StringSplitOptions.RemoveEmptyEntries)
        Return GetBlockByPathStrict(patharray, type, noSoftLink, deep)
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
        Dim lastBlock As IFS1DirBlock = _rootBlock
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
                Dim subblk = _blocksCache(subid)

                If (Not TypeOf subblk Is IFS1BlockWithName) OrElse DirectCast(subblk, IFS1BlockWithName).Name <> subPath Then
                    Continue For
                End If

                If i = pathArray.Length - 1 Then
                    '最后一个了，允许为文件、目录或者符号连接
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
                    '路径中，只能是目录或者符号链接
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

    Public Function Read(path As String, buffer As Byte(), fileOffset As UInt32, bufferOffset As UInt32, count As UInt32) As UInt32
        Dim blk As IFS1FileBlock = GetBlockByPathStrict(path, IFS1Block.BlockType.File)
        Return Read(blk, buffer, fileOffset, bufferOffset, count)
    End Function

    Public Function Read(block As IFS1FileBlock, buffer As Byte(), fileOffset As UInt32, bufferOffset As UInt32, count As UInt32) As UInt32
        If fileOffset >= block.Length Then
            Return 0
        End If

        Dim readlength = 0
        Do While readlength < count
            Dim currentoffset = readlength + fileOffset
            If currentoffset >= block.Length Then
                Return readlength
            End If
            'Dim indexofid = CUInt(Math.Floor(currentoffset / IFS1.DATA_BLOCK_DATA_LEN))
            'Dim offsetofblock = currentoffset Mod IFS1.DATA_BLOCK_DATA_LEN

            Dim offsetofblock As Long
            Dim indexofid = Math.DivRem(currentoffset, IFS1DataBlock.DATA_LENGTH, offsetofblock)

            Dim datablk As IFS1DataBlock = ReadBlockByID(block.SubBlockIDs(indexofid))
            If datablk Is Nothing Then
                Return readlength
            End If

            Dim currentread = Math.Min(Math.Min(Math.Min(IFS1DataBlock.DATA_LENGTH - offsetofblock,
                                count - readlength),
                                 block.Length - currentoffset), datablk.Length - offsetofblock)

            If currentread > 0 Then
                Array.Copy(datablk.data, offsetofblock, buffer, readlength + bufferOffset, currentread)
            Else
                Return readlength
            End If

            readlength += currentread
        Loop
        Return readlength
    End Function

    Private Sub Compress(block As IFS1FileBlock, newSize As UInt32)
        If Options.ReadOnlyMount Then
            Throw New IFS1NoPermissionException()
        End If
        If newSize = block.Length Then
            Return
        End If
        Dim oldsize = block.Length
        If newSize > block.Length Then
            Throw New ArgumentException("newSize")
        End If
        NewTransaction("Compress " + block.Name)

        Dim oldblockcount = CUInt(Math.Ceiling(oldsize / IFS1DataBlock.DATA_LENGTH)),
         newblockcount = CUInt(Math.Ceiling(newSize / IFS1DataBlock.DATA_LENGTH))
        Dim deltacount = oldblockcount - newblockcount

        If deltacount = 0 Then '不需要删除块
            If newSize <= 0 Then
                Return
            End If
            Dim newoffsetofblock = (newSize - 1) Mod IFS1DataBlock.DATA_LENGTH
            If oldblockcount > 0 Then
                Dim lastdatablk As IFS1DataBlock = _blocksCache(block.SubBlockIDs(oldblockcount - 1))
                lastdatablk.Length = newoffsetofblock + 1
                EnqueueBlockChange(lastdatablk)
            End If
        Else
            If oldblockcount > 0 Then
                Dim lastdatablk As IFS1DataBlock = _blocksCache(block.SubBlockIDs(oldblockcount - 1))
                lastdatablk.Length = IFS1DataBlock.DATA_LENGTH
                EnqueueBlockChange(lastdatablk)
            End If
            Dim usedid As New List(Of UInt32)
            For i = newblockcount - 1 + 1 To oldblockcount - 1
                Dim datablk As IFS1DataBlock = _blocksCache(block.SubBlockIDs(i))
                datablk.used = 0
                EnqueueBlockChange(datablk)

                block.SubBlockIDs(i) = INVALID_BLOCK_ID
            Next
        End If

        block.Length = newSize
        EnqueueBlockChange(block)

        CommitTransaction()
    End Sub

    Private Sub Expand(blk As IFS1FileBlock, newSize As UInt32, Optional zeroData As Boolean = False)
        If Options.ReadOnlyMount Then
            Throw New IFS1NoPermissionException()
        End If
        If newSize = blk.Length Then
            Return
        End If
        Dim oldsize = blk.Length
        If newSize < blk.Length Then
            Throw New ArgumentException("newSize")
        End If
        NewTransaction("Expand " + blk.Name)

        Dim oldblockcount = CUInt(Math.Ceiling(oldsize / IFS1DataBlock.DATA_LENGTH)),
         newblockcount = CUInt(Math.Ceiling(newSize / IFS1DataBlock.DATA_LENGTH))
        Dim deltacount = newblockcount - oldblockcount

        If deltacount = 0 Then '不需要新分配块
            If newSize <= 0 Then
                Return
            End If
            Dim newoffsetofblock = (newSize - 1) Mod IFS1DataBlock.DATA_LENGTH
            If oldblockcount > 0 Then
                Dim lastdatablk As IFS1DataBlock = _blocksCache(blk.SubBlockIDs(oldblockcount - 1))
                lastdatablk.Length = newoffsetofblock + 1
                EnqueueBlockChange(lastdatablk)
            End If
        Else
            If oldblockcount > 0 Then
                Dim lastdatablk As IFS1DataBlock = _blocksCache(blk.SubBlockIDs(oldblockcount - 1))
                lastdatablk.Length = IFS1DataBlock.DATA_LENGTH
                EnqueueBlockChange(lastdatablk)
            End If

            If newblockcount > IFS1FileBlock.SUB_BLOCK_COUNT Then
                Throw New IFS1AllocationFailedException("Cannot alloc sub block")
            End If

            Dim newblkids = AllocBlocks(deltacount)

            For i = oldblockcount - 1 + 1 To newblockcount - 1
                Dim newblkid = newblkids(i - (oldblockcount - 1 + 1))

                Dim datablk As New IFS1DataBlock(zeroData)
                datablk.id = newblkid
                If i < newblockcount - 1 Then
                    datablk.Length = IFS1DataBlock.DATA_LENGTH
                Else
                    Dim newoffsetofblock = (newSize - 1) Mod IFS1DataBlock.DATA_LENGTH
                    datablk.Length = newoffsetofblock + 1
                End If

                'ReDim datablk.data(-1)
                datablk.used = 1

                blk.SubBlockIDs(i) = newblkid

                EnqueueBlockChange(datablk)
            Next
        End If
        blk.Length = newSize
        EnqueueBlockChange(blk)

        CommitTransaction()
    End Sub

    Public Sub Resize(block As IFS1FileBlock, newSize As UInt32)
        If Options.ReadOnlyMount Then
            Throw New IFS1NoPermissionException()
        End If
        If newSize > block.Length Then
            Expand(block, newSize)
        ElseIf newSize < block.Length Then
            Compress(block, newSize)
        End If
    End Sub

    Public Function Write(path As String, buffer As Byte(), fileOffset As UInt32, bufferOffset As UInt32, count As UInt32) As UInt32
        If Options.ReadOnlyMount Then
            Throw New IFS1NoPermissionException()
        End If
        Dim blk As IFS1FileBlock = GetBlockByPathStrict(path, IFS1Block.BlockType.File)
        Return Write(blk, buffer, fileOffset, bufferOffset, count)
    End Function

    Public Function Write(block As IFS1FileBlock, buffer As Byte(), fileOffset As UInt32, bufferOffset As UInt32, count As UInt32) As UInt32
        If Options.ReadOnlyMount Then
            Throw New IFS1NoPermissionException()
        End If

        Debug.Assert(count <= buffer.Length)

        If fileOffset + count > block.Length Then
            Expand(block, fileOffset + count)
            Sync()
        End If

        block.LastWriteTime = DateTime.Now
        EnqueueBlockChange(block)

        Dim writtenlength = 0
        Do While writtenlength < count
            Dim currentoffset = writtenlength + fileOffset
            Debug.Assert(currentoffset < block.Length)

            Dim offsetofblock As Long
            Dim indexofid = Math.DivRem(currentoffset, IFS1DataBlock.DATA_LENGTH, offsetofblock)

            Dim datablk As IFS1DataBlock
            ''这里需要先从缓存中读取Block, 否则上次写入的Block会丢失
            'datablk = BlocksCache(blk.blockids(indexofid))
            'If datablk.data.Length <= 0 Then
            datablk = ReadBlockByID(block.SubBlockIDs(indexofid))
            'End If

            Debug.Assert(datablk IsNot Nothing)

            Dim currentwrite = Math.Min(Math.Min(Math.Min(IFS1DataBlock.DATA_LENGTH - offsetofblock,
                                count - writtenlength),
                                 block.Length - currentoffset), datablk.Length - offsetofblock)

            If currentwrite > 0 Then
                Array.Copy(buffer, writtenlength + bufferOffset, datablk.data, offsetofblock, currentwrite)
                EnqueueBlockChange(datablk)
            Else
                Return writtenlength
            End If

            writtenlength += currentwrite
        Loop

        Sync()
        Return writtenlength
    End Function

    Private Sub Unlink(parent As IFS1Block, blockId As UInt32)
        If Options.ReadOnlyMount Then
            Throw New IFS1NoPermissionException()
        End If

        If TypeOf parent Is IFS1BlockWithIDs Then
            Dim blkwithids = DirectCast(parent, IFS1BlockWithIDs)
            Dim index = -1
            For i = 0 To blkwithids.SubBlockIDs.Length - 1
                If blkwithids.SubBlockIDs(i) = blockId Then
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

    Private Sub Link(parent As IFS1Block, blockId As UInt32)
        If Options.ReadOnlyMount Then
            Throw New IFS1NoPermissionException()
        End If

        If TypeOf parent Is IFS1BlockWithIDs Then
            Dim blkwithids = DirectCast(parent, IFS1BlockWithIDs)

            Dim succ As Boolean = False
            For i = 0 To blkwithids.SubBlockIDs.Length - 1
                If Not CheckID(blkwithids.SubBlockIDs(i)) Then
                    blkwithids.SubBlockIDs(i) = blockId
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
        If Options.ReadOnlyMount Then
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
        If Options.ReadOnlyMount Then
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
        If Options.ReadOnlyMount Then
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

    Public Sub CreateSoftLink(path As String, toPath As String)
        If Options.ReadOnlyMount Then
            Throw New IFS1NoPermissionException()
        End If
        If PathExists(path) Then
            Throw New IFS1PathAlreadyExistsException(path)
        End If
        If Not PathExists(toPath) Then
            Throw New IFS1PathNotFoundException(toPath)
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
        newblock.To = toPath

        Link(parent, newblockid)
        EnqueueBlockChange(newblock)

        CommitTransaction()
    End Sub

    Public Sub DeleteSoftLink(path As String)
        'Throw New NotImplementedException("DeleteSoftLink")
        If Options.ReadOnlyMount Then
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
        If Options.ReadOnlyMount Then
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
    ''' <param name="block"></param>
    ''' <remarks></remarks>
    Public Sub ForceDeleteDirBlockRecursive(block As IFS1DirBlock)
        If Options.ReadOnlyMount Then
            Throw New IFS1NoPermissionException()
        End If
        For i = 0 To block.SubBlockIDs.Length - 1
            If Not CheckID(block.SubBlockIDs(i)) Then
                Exit For
            End If
            Dim subblk = _blocksCache(block.SubBlockIDs(i))
            If subblk.type = IFS1Block.BlockType.Dir Then
                ForceDeleteDirBlockRecursive(subblk)
            ElseIf subblk.type = IFS1Block.BlockType.File Then
                ForceDeleteFileBlock(subblk)
            Else
                subblk.used = 0
                EnqueueBlockChange(subblk)
            End If
            block.SubBlockIDs(i) = INVALID_BLOCK_ID
        Next

        block.used = 0
        EnqueueBlockChange(block)
    End Sub

    ''' <summary>
    ''' 使用需谨慎, 请务必先解除和别的Block的联系
    ''' </summary>
    ''' <param name="block"></param>
    ''' <remarks></remarks>
    Private Sub ForceDeleteFileBlock(block As IFS1FileBlock)
        If Options.ReadOnlyMount Then
            Throw New IFS1NoPermissionException()
        End If
        For i = 0 To block.SubBlockIDs.Length - 1
            If Not CheckID(block.SubBlockIDs(i)) Then
                Exit For
            End If
            Dim subblk = _blocksCache(block.SubBlockIDs(i))
            subblk.used = 0
            EnqueueBlockChange(subblk)
            block.SubBlockIDs(i) = INVALID_BLOCK_ID
        Next

        block.used = 0
        EnqueueBlockChange(block)
    End Sub

    Public Sub DeleteFile(path As String)
        If Options.ReadOnlyMount Then
            Throw New IFS1NoPermissionException()
        End If
        path = path.Replace("\"c, "/"c)
        Dim pathArray = path.Split({"/"c}, StringSplitOptions.RemoveEmptyEntries)
        Dim filename = pathArray.Last
        ReDim Preserve pathArray(pathArray.Length - 2)
        Dim parent As IFS1DirBlock = GetBlockByPathStrict(pathArray, IFS1Block.BlockType.Dir)
        Dim blk As IFS1FileBlock = GetBlockByPathStrict(path, IFS1Block.BlockType.File, True)

        ForceDeleteFileBlock(blk)

        Unlink(parent, blk.id)

        EnqueueBlockChange(blk)
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

    Public Sub SeekBlock(id As UInt32, origin As SeekOrigin)
        _s.Seek(id * IFS1Block.BLOCK_LENGTH, origin)
    End Sub

    Public HeaderLength As New Dictionary(Of IFS1Block.BlockType, Int32) From {
        {IFS1Block.BlockType.Raw, 512},
        {IFS1Block.BlockType.Data, 512},
        {IFS1Block.BlockType.File, 65536},
        {IFS1Block.BlockType.Dir, 65536},
        {IFS1Block.BlockType.SoftLink, 65536}
    }
    Public Const FIRST_READ_LEN = 512

    Private _cacheStream As New MemoryStream(IFS1Block.BLOCK_LENGTH)

    Public Function ReadBlock(Optional readdata As Boolean = True) As IFS1Block
        If _s.Length - _s.Position < IFS1Block.BLOCK_LENGTH Then
            Return Nothing
        End If

        SyncLock _cacheStream

            _cacheStream.Seek(0, SeekOrigin.Begin)

            '读取公共部分
            Dim buffer1(FIRST_READ_LEN - 1) As Byte
            BinaryHelper.SafeRead(_s, buffer1, 0, buffer1.Length)
            _cacheStream.Write(buffer1, 0, buffer1.Length)

            _cacheStream.Seek(4, SeekOrigin.Begin) 'skip used
            Dim type = BinaryHelper.ReadInt32LE(_cacheStream)
            _cacheStream.Seek(FIRST_READ_LEN, SeekOrigin.Begin) 'back

            '读取剩下的
            If readdata Then '读取数据的话就把整个Block读取
                Dim buffer2(IFS1Block.BLOCK_LENGTH - FIRST_READ_LEN - 1) As Byte
                BinaryHelper.SafeRead(_s, buffer2, 0, buffer2.Length)
                _cacheStream.Write(buffer2, 0, buffer2.Length)
            ElseIf HeaderLength.ContainsKey(type) Then  '否则只读取头部分
                Dim buffer2(HeaderLength(type) - FIRST_READ_LEN - 1) As Byte
                BinaryHelper.SafeRead(_s, buffer2, 0, buffer2.Length)
                _cacheStream.Write(buffer2, 0, buffer2.Length)
            End If
            _s.Seek(IFS1Block.BLOCK_LENGTH - _cacheStream.Position, SeekOrigin.Current)
            _cacheStream.Seek(0, SeekOrigin.Begin)
            '_s.Seek(4, SeekOrigin.Current) 'skip used
            'Dim type = BinaryHelper.ReadInt32LE(_s)
            '_s.Seek(-8, SeekOrigin.Current) 'back
            Select Case type
                Case IFS1Block.BlockType.Raw
                    If readdata Then
                        Return IFS1Block.Read(_cacheStream)
                    Else
                        Return IFS1Block.ReadWithoutData(_cacheStream)
                    End If
                Case IFS1Block.BlockType.File
                    Return IFS1FileBlock.Read(_cacheStream)
                Case IFS1Block.BlockType.Data
                    If readdata Then
                        Return IFS1DataBlock.Read(_cacheStream)
                    Else
                        Return IFS1DataBlock.ReadWithoutData(_cacheStream)
                    End If
                Case IFS1Block.BlockType.Dir
                    Return IFS1DirBlock.Read(_cacheStream)
                Case IFS1Block.BlockType.SoftLink
                    Return IFS1SoftLinkBlock.Read(_cacheStream)
                Case Else
                    Logger.WriteLine("Warning: Unknown block! Reading as unused RawBlock")
                    Dim blk As IFS1Block
                    If readdata Then
                        blk = IFS1Block.Read(_cacheStream)
                    Else
                        blk = IFS1Block.ReadWithoutData(_cacheStream)
                    End If
                    blk.used = 0
                    blk.type = IFS1Block.BlockType.Raw
                    Return blk
            End Select
        End SyncLock
    End Function

    Public Sub WriteBlock(block As IFS1Block)
        If Options.ReadOnlyMount Then
            Throw New IFS1NoPermissionException()
        End If

        SyncLock _cacheStream
            _cacheStream.Seek(0, SeekOrigin.Begin)
            block.Write(_cacheStream, False)
            SeekBlock(block.id, SeekOrigin.Begin)
            Dim data = _cacheStream.ToArray()
            _s.Write(data, 0, data.Length)
        End SyncLock

    End Sub

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

    Public Function Check(repair As Boolean) As Boolean
        If repair AndAlso Options.ReadOnlyMount Then
            Throw New IFS1NoPermissionException("Cannot repair when readonly mounting")
        End If

        NewTransaction("Repair")

        '检查RootBlock
        If _rootBlock.type <> IFS1Block.BlockType.Dir Then
            Logger.WriteLine("RootBlock.Type != Dir, check failed!")
            Throw New IFS1BadFileSystemException("Type mismatch!")
        End If
        If _rootBlock.Name <> "/" Then
            Logger.WriteLine("RootBlock.Name != ""/""!")
            _rootBlock.Name = "/"
            EnqueueBlockChange(_rootBlock)
        End If

        '检查是否有未被引用的标记为已使用的块
        Dim refed(_blocksCache.Count - 1) As Boolean
        For i = FIRST_BLOCK_ID To _blocksCache.Count - 1
            refed(i) = False
        Next
        For i = FIRST_BLOCK_ID To _blocksCache.Count - 1
            Dim blk = _blocksCache(i)

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
                    Logger.WriteLine("Block " + i.ToString() + ".name=""""!")
                    If repair Then
                        blkwithname.Name = "block" + i.ToString()
                        EnqueueBlockChange(blkwithname)
                    End If
                End If
            End If

            If TypeOf blk Is IFS1BlockWithTime Then
                Dim blkwithtime = DirectCast(blk, IFS1BlockWithTime)
                If blkwithtime.CreationTime > DateTime.Now Then
                    Logger.WriteLine("Block " + i.ToString() + ".CreationTime>Now!")
                End If
                If blkwithtime.LastAccessTime > DateTime.Now Then
                    Logger.WriteLine("Block " + i.ToString() + ".LastAccessTime>Now!")
                End If
                If blkwithtime.LastWriteTime > DateTime.Now Then
                    Logger.WriteLine("Block " + i.ToString() + ".LastWriteTime>Now!")
                End If

                Try
                    blkwithtime.CreationTime.ToFileTimeUtc()
                Catch ex As Exception
                    Logger.WriteLine("Block " + i.ToString() + ".CreationTime wrong!")
                    If repair Then
                        blkwithtime.CreationTime = DateTime.Now
                        EnqueueBlockChange(blkwithtime)
                    End If
                End Try
                Try
                    blkwithtime.LastAccessTime.ToFileTimeUtc()
                Catch ex As Exception
                    Logger.WriteLine("Block " + i.ToString() + ".LastAccessTime wrong!")
                    If repair Then
                        blkwithtime.LastAccessTime = DateTime.Now
                        EnqueueBlockChange(blkwithtime)
                    End If
                End Try
                Try
                    blkwithtime.LastWriteTime.ToFileTimeUtc()
                Catch ex As Exception
                    Logger.WriteLine("Block " + i.ToString() + ".LastWriteTime wrong!")
                    If repair Then
                        blkwithtime.LastWriteTime = DateTime.Now
                        EnqueueBlockChange(blkwithtime)
                    End If
                End Try
            End If

            If TypeOf blk Is IFS1SoftLinkBlock Then
                Dim softlink = DirectCast(blk, IFS1SoftLinkBlock)
                If softlink.To.StartsWith("DA:/") Then
                    Logger.WriteLine("Block " + i.ToString() + ".To Starts With ""DA:/"", changing to ""/""")
                    softlink.To = softlink.To.Substring(4)
                End If
                If softlink.To Is Nothing Then
                    Logger.WriteLine("CANNOT REPAIR: Block " + i.ToString() + ".To is nothing!")
                ElseIf Not PathExists(softlink.To) Then
                    Logger.WriteLine("CANNOT REPAIR: Block " + i.ToString() + ".To Not Exists!")
                End If
            End If
        Next

        Dim flag = True
        For i = FIRST_BLOCK_ID + 1 To _blocksCache.Count - 1
            If _blocksCache(i).used <> 0 AndAlso Not refed(i) Then
                Logger.WriteLine("Block " + i.ToString() + " not refed but used!")
                If repair Then
                    _blocksCache(i).used = 0
                    EnqueueBlockChange(_blocksCache(i))
                End If
                flag = False
            End If
        Next
        CommitTransaction()
        Sync()
        Return flag
    End Function

    Public Shared Sub Make(filename As String, length As ULong, writePreserve As Boolean, Optional mbr As MBR = Nothing)
        Using fs As New FileStream(filename, FileMode.Create, FileAccess.Write)
            Make(fs, length, writePreserve, mbr)
        End Using
    End Sub

    Public Shared Sub Make(s As Stream, length As ULong, writePreserve As Boolean, Optional mbr As MBR = Nothing)
        Dim Logger As New LoggerWrapper(Console.Out)

        Dim blockcount = Math.Floor(length / IFS1Block.BLOCK_LENGTH)

        If mbr IsNot Nothing Then
            Dim sec0 = BinaryHelper.StructToBytes(Of MBR)(mbr)
            s.Write(sec0, 0, sec0.Length)
        Else
            Dim sec0(512 - 1) As Byte
            sec0(510) = &H55
            sec0(511) = &HAA
            s.Write(sec0, 0, sec0.Length)
        End If

        Dim rootblock As New IFS1DirBlock
        rootblock.Name = "/"
        rootblock.used = 1
        For i = 0 To rootblock.SubBlockIDs.Length - 1
            rootblock.SubBlockIDs(i) = INVALID_BLOCK_ID
        Next
        s.Seek(FIRST_BLOCK_ID * IFS1Block.BLOCK_LENGTH, SeekOrigin.Begin)
        Using ms As New MemoryStream(IFS1Block.BLOCK_LENGTH)
            rootblock.Write(ms, True)
            If writePreserve Then
                ms.Seek(511, SeekOrigin.Begin)
                ms.WriteByte(0)
            End If
            Dim data = ms.ToArray()
            s.Write(data, 0, data.Length)
        End Using


        Logger.Write("Writing Blocks 1/{0}", blockcount)

        Dim sw As New Stopwatch
        sw.Start()
        For i = FIRST_BLOCK_ID + 1 To blockcount - 1
            Dim block As New IFS1Block
            block.used = 0
            s.Seek(i * IFS1Block.BLOCK_LENGTH, SeekOrigin.Begin)
            Using ms As New MemoryStream(IFS1Block.BLOCK_LENGTH)
                block.Write(ms, True)
                If writePreserve Then
                    ms.Seek(511, SeekOrigin.Begin)
                    ms.WriteByte(0)
                End If
                Dim data = ms.ToArray()
                s.Write(data, 0, data.Length)
            End Using
            If i Mod 233 = 0 OrElse i = blockcount - 1 Then
                Console.Write("{0}Writing Blocks {1}/{2}                      ", Chr(13), i - FIRST_BLOCK_ID + 1, blockcount)
            End If
        Next
        sw.Stop()
        Console.WriteLine()
        Logger.WriteLine("done {0}ms", sw.ElapsedMilliseconds)
        s.Flush()
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
