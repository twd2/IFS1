Imports Dokan
Imports IFS1Common
Imports System.IO

Public Class IFS1Driver
    Implements DokanOperations

    Private ifs As IFS1

    Public Sub New(ifs As IFS1)
        Me.ifs = ifs
    End Sub

    Public Function Cleanup(filename As String, info As DokanFileInfo) As Integer Implements DokanOperations.Cleanup
        'Console.WriteLine("Cleanup: " + filename)
        ifs.Sync()
        Return 0
    End Function

    Public Function CloseFile(filename As String, info As DokanFileInfo) As Integer Implements DokanOperations.CloseFile
        Console.WriteLine("CloseFile: " + filename)
        ifs.Sync()
        Return 0
    End Function

    Public Function CreateDirectory(filename As String, info As DokanFileInfo) As Integer Implements DokanOperations.CreateDirectory
        Try
            Console.WriteLine("CreateDirectory: " + filename)
            ifs.CreateDir(filename)
            ifs.Sync()
            Return 0
        Catch ex As Exception
            Return exceptionToDokanCode(ex)
        End Try
    End Function

    Public Function CreateFile(filename As String, access As IO.FileAccess, share As IO.FileShare, mode As IO.FileMode, options As IO.FileOptions, info As DokanFileInfo) As Integer Implements DokanOperations.CreateFile
        Try
            Console.WriteLine("CreateFile: " + filename)
            If ifs.DirExists(filename) Then
                info.IsDirectory = True
                Return 0
            End If
            If mode = FileMode.CreateNew AndAlso ifs.FileExists(filename) Then
                Return -DokanNet.ERROR_ALREADY_EXISTS
            End If
            If mode = FileMode.Open AndAlso Not ifs.FileExists(filename) Then
                Return -DokanNet.ERROR_FILE_NOT_FOUND
            End If
            If Not ifs.FileExists(filename) Then
                ifs.CreateEmptyFile(filename)
                ifs.Sync()
            End If
            Return 0
        Catch ex As Exception
            Return exceptionToDokanCode(ex)
        End Try
    End Function

    Public Function DeleteDirectory(filename As String, info As DokanFileInfo) As Integer Implements DokanOperations.DeleteDirectory
        Try
            Console.WriteLine("DeleteDirectory: " + filename)
            Try
                ifs.DeleteDir(filename)
                ifs.Sync()
            Catch ex0 As Exception
                Console.WriteLine("DeleteDirectory(SoftLink): " + filename)
                ifs.DeleteSoftLink(filename)
                ifs.Sync()
            End Try
            Return 0
        Catch ex As Exception
            Return exceptionToDokanCode(ex)
        End Try
    End Function

    Public Function DeleteFile(filename As String, info As DokanFileInfo) As Integer Implements DokanOperations.DeleteFile
        Try
            Console.WriteLine("DeleteFile: " + filename)
            Try
                ifs.DeleteFile(filename)
                ifs.Sync()
            Catch ex0 As Exception
                Console.WriteLine("DeleteFile(SoftLink): " + filename)
                ifs.DeleteSoftLink(filename)
                ifs.Sync()
            End Try
            Return 0
        Catch ex As Exception
            Return exceptionToDokanCode(ex)
        End Try
    End Function

    Public Function FindFiles(filename As String, files As ArrayList, info As DokanFileInfo) As Integer Implements DokanOperations.FindFiles
        Try
            filename = filename.Replace("\"c, "/"c)
            If filename.Last <> "/" Then
                filename += "/"
            End If
            Console.WriteLine("FindFiles: " + filename)
            Dim blk As IFS1DirBlock = ifs.GetBlockByPathStrict(filename)
            Dim subblks = ifs.GetSubBlocks(blk)
            For Each subblk In subblks
                files.Add(GetBlockInfo(subblk))
            Next
            Return 0
        Catch ex As Exception
            Return exceptionToDokanCode(ex)
        End Try
    End Function

    Public Function FlushFileBuffers(filename As String, info As DokanFileInfo) As Integer Implements DokanOperations.FlushFileBuffers
        ifs.Sync()
        Return 0
    End Function

    Public Function GetDiskFreeSpace(ByRef freeBytesAvailable As ULong, ByRef totalBytes As ULong, ByRef totalFreeBytes As ULong, info As DokanFileInfo) As Integer Implements DokanOperations.GetDiskFreeSpace
        totalBytes = ifs.CountTotalBlocks() * IFS1.BLOCK_LEN
        freeBytesAvailable = totalBytes - ifs.CountUsedBlocks() * IFS1.BLOCK_LEN
        totalFreeBytes = freeBytesAvailable
        Return 0
    End Function

    Public Function GetFileInformation(filename As String, fi As FileInformation, info As DokanFileInfo) As Integer Implements DokanOperations.GetFileInformation
        Try
            Console.WriteLine("GetFileInformation: " + filename)
            Dim blk = ifs.GetBlockByPath(filename)

            GetBlockInfo(fi, blk)

            Return 0
        Catch ex As Exception
            Return exceptionToDokanCode(ex)
        End Try
    End Function

    Public Function LockFile(filename As String, offset As Long, length As Long, info As DokanFileInfo) As Integer Implements DokanOperations.LockFile
        'TODO
        Return -DokanNet.ERROR_ACCESS_DENIED
    End Function

    Public Function MoveFile(filename As String, newname As String, replace As Boolean, info As DokanFileInfo) As Integer Implements DokanOperations.MoveFile
        Try
            Console.WriteLine("MoveFile: " + filename)
            ifs.Move(filename, newname)
            ifs.Sync()
            Return 0
        Catch ex As Exception
            Return exceptionToDokanCode(ex)
        End Try
    End Function

    Public Function OpenDirectory(filename As String, info As DokanFileInfo) As Integer Implements DokanOperations.OpenDirectory
        If ifs.DirExists(filename) Then
            Return 0
        Else
            Return -DokanNet.ERROR_PATH_NOT_FOUND
        End If
    End Function

    Public Function ReadFile(filename As String, buffer() As Byte, ByRef readBytes As UInteger, offset As Long, info As DokanFileInfo) As Integer Implements DokanOperations.ReadFile
        Try
            'Console.WriteLine("ReadFile: " + filename)
            readBytes = ifs.Read(filename, buffer, offset, 0, buffer.Length)
            Return 0
        Catch ex As Exception
            Return exceptionToDokanCode(ex)
        End Try
    End Function

    Public Function SetAllocationSize(filename As String, length As Long, info As DokanFileInfo) As Integer Implements DokanOperations.SetAllocationSize
        Try
            Console.WriteLine("SetAllocationSize: " + filename)
            Dim fileblk = ifs.GetBlockByPathStrict(filename, IFS1Block.BlockType.File)
            ifs.Resize(fileblk, length)
            ifs.Sync()
            Return 0
        Catch ex As Exception
            Return exceptionToDokanCode(ex)
        End Try
    End Function

    Public Function SetEndOfFile(filename As String, length As Long, info As DokanFileInfo) As Integer Implements DokanOperations.SetEndOfFile
        Try
            Console.WriteLine("SetEndOfFile: " + filename)
            Dim fileblk = ifs.GetBlockByPathStrict(filename, IFS1Block.BlockType.File)
            ifs.Resize(fileblk, length)
            ifs.Sync()
            Return 0
        Catch ex As Exception
            Return exceptionToDokanCode(ex)
        End Try
    End Function

    Public Function SetFileAttributes(filename As String, attr As IO.FileAttributes, info As DokanFileInfo) As Integer Implements DokanOperations.SetFileAttributes
        'TODO
        Return -DokanNet.ERROR_ACCESS_DENIED
    End Function

    Public Function SetFileTime(filename As String, ctime As Date?, atime As Date?, mtime As Date?, info As DokanFileInfo) As Integer Implements DokanOperations.SetFileTime
        Try
            Console.WriteLine("SetFileTime: " + filename)
            ifs.SetTime(filename, ctime, atime, mtime)
            ifs.Sync()
            Return 0
        Catch ex As Exception
            Return exceptionToDokanCode(ex)
        End Try
    End Function

    Public Function UnlockFile(filename As String, offset As Long, length As Long, info As DokanFileInfo) As Integer Implements DokanOperations.UnlockFile
        'TODO
        Return -DokanNet.ERROR_ACCESS_DENIED
    End Function

    Public Function Unmount(info As DokanFileInfo) As Integer Implements DokanOperations.Unmount
        Return 0
    End Function

    Public Function WriteFile(filename As String, buffer() As Byte, ByRef writtenBytes As UInteger, offset As Long, info As DokanFileInfo) As Integer Implements DokanOperations.WriteFile
        Try
            'Console.WriteLine("WriteFile: " + filename)
            writtenBytes = ifs.Write(filename, buffer, offset, 0, buffer.Length)
            Return 0
        Catch ex As Exception
            Return exceptionToDokanCode(ex)
        End Try
    End Function

    Private Function GetBlockInfo(blk As IFS1Block) As FileInformation
        Dim fi As New FileInformation
        GetBlockInfo(fi, blk)
        Return fi
    End Function

    Private Sub GetBlockInfo(fi As FileInformation, blk As IFS1Block)
        If ifs.ReadOnlyMount Then
            fi.Attributes = fi.Attributes Or FileAttributes.ReadOnly
        End If

        If blk.type = IFS1Block.BlockType.SoftLink Then
            Dim softlink = DirectCast(blk, IFS1SoftLinkBlock)
            Try
                GetBlockInfo(fi, ifs.GetBlockByPath(softlink.To))
            Catch ex As Exception
                fi.Length = IFS1.BLOCK_LEN
                fi.Attributes = FileAttributes.Hidden
                fi.CreationTime = DateTime.Now
                fi.LastAccessTime = DateTime.Now
                fi.LastWriteTime = DateTime.Now
            End Try
            fi.FileName = softlink.Name
            Return
        End If

        If Not TypeOf blk Is IFS1BlockWithName Then
            Throw New ArgumentException("blk")
        Else
            fi.FileName = DirectCast(blk, IFS1BlockWithName).Name
        End If

        If TypeOf blk Is IFS1BlockWithTime Then
            Dim blkwithtime = DirectCast(blk, IFS1BlockWithTime)
            fi.CreationTime = blkwithtime.CreationTime
            fi.LastAccessTime = blkwithtime.LastAccessTime
            fi.LastWriteTime = blkwithtime.LastWriteTime
            'Dim a = fi.CreationTime.ToFileTimeUtc()
            'a = fi.LastAccessTime.ToFileTimeUtc()
            'a = fi.LastWriteTime.ToFileTimeUtc()
        End If

        If TypeOf blk Is IFS1BlockWithLength Then
            Dim blkwithlength = DirectCast(blk, IFS1BlockWithLength)
            fi.Length = blkwithlength.Length
        Else
            fi.Length = IFS1.BLOCK_LEN
        End If

        If blk.type = IFS1Block.BlockType.Dir Then
            fi.Attributes = fi.Attributes Or FileAttributes.Directory
        Else
            fi.Attributes = fi.Attributes Or FileAttributes.Normal
        End If
    End Sub

    Public Function exceptionToDokanCode(ex As Exception) As Int32
        If TypeOf ex Is IFS1AllocationFailedException Then
            Return -113
        ElseIf TypeOf ex Is IFS1BadFileSystemException Then
            Return -114
        ElseIf TypeOf ex Is IFS1DirAlreadyExistsException Then
            Return -1760
        ElseIf TypeOf ex Is IFS1FileAlreadyExistsException Then
            Return -1760
        ElseIf TypeOf ex Is IFS1FileNotFoundException Then
            Return -2
        ElseIf TypeOf ex Is IFS1NoPermissionException Then
            Return -5
        ElseIf TypeOf ex Is IFS1PathNotFoundException Then
            Return -3
        ElseIf TypeOf ex Is IFS1TransactionCommittedException Then
            Return -1
        ElseIf TypeOf ex Is IFS1BadPathException Then
            Return -123
        ElseIf TypeOf ex Is IFS1Exception Then
            Return -100000
        Else
            Console.WriteLine(ex.ToString())
            Throw ex
        End If
    End Function
End Class

