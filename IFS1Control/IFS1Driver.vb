Imports DokanNet
Imports IFS1Common
Imports System.IO

Public Class IFS1Driver
    Implements IDokanOperations

    Private ifs As IFS1

    Public Sub New(ifs As IFS1)
        Me.ifs = ifs
    End Sub

    Public Function Cleanup(filename As String, info As DokanFileInfo) As DokanError Implements IDokanOperations.Cleanup
        ' ifs.Logger.WriteLine("Cleanup: " + filename)
        ifs.Sync()
        Return DokanError.ErrorSuccess
    End Function

    Public Function CloseFile(filename As String, info As DokanFileInfo) As DokanError Implements IDokanOperations.CloseFile
        ifs.Logger.WriteLine("CloseFile: " + filename)
        ifs.Sync()
        Return DokanError.ErrorSuccess
    End Function

    Public Function CreateDirectory(filename As String, info As DokanFileInfo) As DokanError Implements IDokanOperations.CreateDirectory
        Try
            ifs.Logger.WriteLine("CreateDirectory: " + filename)
            ifs.CreateDir(filename)
            ifs.Sync()
            Return DokanError.ErrorSuccess
        Catch ex As Exception
            Return exceptionToDokanCode(ex)
        End Try
    End Function

    Public Function CreateFile(filename As String, access As DokanNet.FileAccess, share As IO.FileShare, mode As IO.FileMode, options As IO.FileOptions, attributes As IO.FileAttributes, info As DokanFileInfo) As DokanError Implements IDokanOperations.CreateFile
        Try
            ifs.Logger.WriteLine("CreateFile: " + filename)
            If ifs.DirExists(filename) Then
                info.IsDirectory = True
                Return DokanError.ErrorSuccess
            End If
            If mode = FileMode.CreateNew AndAlso ifs.FileExists(filename) Then
                Return DokanError.ErrorAlreadyExists
            End If
            If mode = FileMode.Open AndAlso Not ifs.FileExists(filename) Then
                Return DokanError.ErrorFileNotFound
            End If
            If Not ifs.FileExists(filename) Then
                ifs.CreateEmptyFile(filename)
                ifs.Sync()
            End If
            Return DokanError.ErrorSuccess
        Catch ex As Exception
            Return exceptionToDokanCode(ex)
        End Try
    End Function

    Public Function DeleteDirectory(filename As String, info As DokanFileInfo) As DokanError Implements IDokanOperations.DeleteDirectory
        Try
            ifs.Logger.WriteLine("DeleteDirectory: " + filename)
            Try
                ifs.DeleteDir(filename)
                ifs.Sync()
            Catch ex0 As Exception
                ifs.Logger.WriteLine("DeleteDirectory(SoftLink): " + filename)
                ifs.DeleteSoftLink(filename)
                ifs.Sync()
            End Try
            Return DokanError.ErrorSuccess
        Catch ex As Exception
            Return exceptionToDokanCode(ex)
        End Try
    End Function

    Public Function DeleteFile(filename As String, info As DokanFileInfo) As DokanError Implements IDokanOperations.DeleteFile
        Try
            ifs.Logger.WriteLine("DeleteFile: " + filename)
            Try
                ifs.DeleteFile(filename)
                ifs.Sync()
            Catch ex0 As Exception
                ifs.Logger.WriteLine("DeleteFile(SoftLink): " + filename)
                ifs.DeleteSoftLink(filename)
                ifs.Sync()
            End Try
            Return DokanError.ErrorSuccess
        Catch ex As Exception
            Return exceptionToDokanCode(ex)
        End Try
    End Function

    Public Function FindFiles(filename As String, ByRef files As IList(Of FileInformation), info As DokanFileInfo) As DokanError Implements IDokanOperations.FindFiles
        Try
            files = New List(Of FileInformation)
            ifs.Logger.WriteLine("FindFiles: " + filename)
            Dim blk As IFS1DirBlock = ifs.GetBlockByPath(filename)
            Dim subblks = ifs.GetSubBlocks(blk)
            For Each subblk In subblks
                files.Add(GetBlockInfo(subblk))
            Next
            Return DokanError.ErrorSuccess
        Catch ex As Exception
            Return exceptionToDokanCode(ex)
        End Try
    End Function

    Public Function FlushFileBuffers(filename As String, info As DokanFileInfo) As DokanError Implements IDokanOperations.FlushFileBuffers
        ifs.Sync()
        Return DokanError.ErrorSuccess
    End Function

    Public Function GetDiskFreeSpace(ByRef freeBytesAvailable As Long, ByRef totalBytes As Long, ByRef used As Long, info As DokanFileInfo) As DokanError Implements IDokanOperations.GetDiskFreeSpace
        totalBytes = ifs.CountTotalBlocks() * IFS1.BLOCK_LEN
        used = ifs.CountUsedBlocks() * IFS1.BLOCK_LEN
        freeBytesAvailable = totalBytes - used
        Return DokanError.ErrorSuccess
    End Function

    Public Function GetFileInformation(filename As String, ByRef fi As FileInformation, info As DokanFileInfo) As DokanError Implements IDokanOperations.GetFileInformation
        Try
            ifs.Logger.WriteLine("GetFileInformation: " + filename)
            Dim blk = ifs.GetBlockByPath(filename)

            GetBlockInfo(fi, blk)

            Return DokanError.ErrorSuccess
        Catch ex As Exception
            Return exceptionToDokanCode(ex)
        End Try
    End Function

    Public Function LockFile(filename As String, offset As Long, length As Long, info As DokanFileInfo) As DokanError Implements IDokanOperations.LockFile
        'TODO
        Return DokanError.ErrorAccessDenied
    End Function

    Public Function MoveFile(filename As String, newname As String, replace As Boolean, info As DokanFileInfo) As DokanError Implements IDokanOperations.MoveFile
        Try
            ifs.Logger.WriteLine("MoveFile: " + filename)
            ifs.Move(filename, newname)
            ifs.Sync()
            Return DokanError.ErrorSuccess
        Catch ex As Exception
            Return exceptionToDokanCode(ex)
        End Try
    End Function

    Public Function OpenDirectory(filename As String, info As DokanFileInfo) As DokanError Implements IDokanOperations.OpenDirectory
        If ifs.DirExists(filename) Then
            Return DokanError.ErrorSuccess
        Else
            Return DokanError.ErrorPathNotFound
        End If
    End Function

    Public Function ReadFile(filename As String, buffer() As Byte, ByRef readBytes As Integer, offset As Long, info As DokanFileInfo) As DokanError Implements IDokanOperations.ReadFile
        Try
            'ifs.Logger.WriteLine("ReadFile: " + filename)
            readBytes = ifs.Read(filename, buffer, offset, 0, buffer.Length)
            Return DokanError.ErrorSuccess
        Catch ex As Exception
            Return exceptionToDokanCode(ex)
        End Try
    End Function

    Public Function SetAllocationSize(filename As String, length As Long, info As DokanFileInfo) As DokanError Implements IDokanOperations.SetAllocationSize
        Try
            ifs.Logger.WriteLine("SetAllocationSize: " + filename)
            Dim fileblk = ifs.GetBlockByPathStrict(filename, IFS1Block.BlockType.File)
            ifs.Resize(fileblk, length)
            ifs.Sync()
            Return DokanError.ErrorSuccess
        Catch ex As Exception
            Return exceptionToDokanCode(ex)
        End Try
    End Function

    Public Function SetEndOfFile(filename As String, length As Long, info As DokanFileInfo) As DokanError Implements IDokanOperations.SetEndOfFile
        Try
            ifs.Logger.WriteLine("SetEndOfFile: " + filename)
            Dim fileblk = ifs.GetBlockByPathStrict(filename, IFS1Block.BlockType.File)
            ifs.Resize(fileblk, length)
            ifs.Sync()
            Return DokanError.ErrorSuccess
        Catch ex As Exception
            Return exceptionToDokanCode(ex)
        End Try
    End Function

    Public Function SetFileAttributes(filename As String, attr As IO.FileAttributes, info As DokanFileInfo) As DokanError Implements IDokanOperations.SetFileAttributes
        'TODO
        Return DokanError.ErrorAccessDenied
    End Function

    Public Function SetFileTime(filename As String, ctime As Date?, atime As Date?, mtime As Date?, info As DokanFileInfo) As DokanError Implements IDokanOperations.SetFileTime
        Try
            ifs.Logger.WriteLine("SetFileTime: " + filename)
            ifs.SetTime(filename, ctime, atime, mtime)
            ifs.Sync()
            Return DokanError.ErrorSuccess
        Catch ex As Exception
            Return exceptionToDokanCode(ex)
        End Try
    End Function

    Public Function UnlockFile(filename As String, offset As Long, length As Long, info As DokanFileInfo) As DokanError Implements IDokanOperations.UnlockFile
        'TODO
        Return DokanError.ErrorAccessDenied
    End Function

    Public Function Unmount(info As DokanFileInfo) As DokanError Implements IDokanOperations.Unmount
        Return DokanError.ErrorSuccess
    End Function

    Public Function WriteFile(filename As String, buffer() As Byte, ByRef writtenBytes As Integer, offset As Long, info As DokanFileInfo) As DokanError Implements IDokanOperations.WriteFile
        Try
            'Console.WriteLine("WriteFile: " + filename)
            writtenBytes = ifs.Write(filename, buffer, offset, 0, buffer.Length)
            Return DokanError.ErrorSuccess
        Catch ex As Exception
            Return exceptionToDokanCode(ex)
        End Try
    End Function

    Private Function GetBlockInfo(blk As IFS1Block) As FileInformation
        Dim fi As New FileInformation
        GetBlockInfo(fi, blk)
        Return fi
    End Function

    Private Sub GetBlockInfo(ByRef fi As FileInformation, blk As IFS1Block)
        If ifs.opt.ReadOnlyMount Then
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

    Public Function GetFileSecurity(fileName As String, ByRef security As System.Security.AccessControl.FileSystemSecurity, sections As System.Security.AccessControl.AccessControlSections, info As DokanFileInfo) As DokanError Implements IDokanOperations.GetFileSecurity
        ifs.Logger.WriteLine("GetFileSecurity: " + fileName)
        security = Nothing
        Return DokanError.ErrorError
    End Function

    Public Function SetFileSecurity(fileName As String, security As System.Security.AccessControl.FileSystemSecurity, sections As System.Security.AccessControl.AccessControlSections, info As DokanFileInfo) As DokanNet.DokanError Implements IDokanOperations.SetFileSecurity
        ifs.Logger.WriteLine("SetFileSecurity: " + fileName)
        Return DokanError.ErrorError
    End Function

    Public Function GetVolumeInformation(ByRef volumeLabel As String, ByRef features As FileSystemFeatures, ByRef fileSystemName As String, info As DokanFileInfo) As DokanError Implements IDokanOperations.GetVolumeInformation
        volumeLabel = "IFS1"
        fileSystemName = "IFS1"
        Return DokanError.ErrorSuccess
    End Function

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

