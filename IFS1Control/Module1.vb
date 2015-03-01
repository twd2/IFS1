Imports IFS1Common
Imports System.IO
Imports Dokan
Imports System.Threading

Module Module1

    Public logger As New LoggerWrapper(Console.Out)

    Sub PrintUsage()
        Console.WriteLine("ifs1 <command> [<arguments>]")
        Console.WriteLine("Commands:")
        Console.WriteLine("{0}mount, m -dfrs{0}Mount IFS1", vbTab)
        Console.WriteLine("{0}mkfs, makefs -dfl{0}Make file/device IFS1", vbTab)
        Console.WriteLine("Arguments:")
        Console.WriteLine("{0}-c, --check{0}{0}Check when mount", vbTab)
        Console.WriteLine("{0}-f, --file filename; -d, --dev [A-Z]:\{0}File or device name", vbTab)
        Console.WriteLine("{0}-m, --mountpoint [A-Z]{0}{0}Mount point", vbTab)
        Console.WriteLine("{0}-l, --length length(B/K/M/G/T){0}{0}Length for mkfs", vbTab)
        Console.WriteLine("{0}-r, --readonly{0}{0}Readonly Mount", vbTab)
        Console.WriteLine("{0}-p, --repair{0}{0}Repair if FS check failed", vbTab)
        Console.ReadKey()
    End Sub

    Public Sub Mount(args As String())
        Dim parser As New StartupArgsParser()
        parser.AddArgument("Check", "c", "check", 0, 0)
        parser.AddArgument("ReadOnly", "r", "readonly", 0, 0)
        parser.AddArgument("Repair", "p", "repair", 0, 0)
        parser.AddArgument("Device", "d", "device", 1, 1)
        parser.AddArgument("Filename", "f", "file", 1, 1)
        parser.AddArgument("MountPoint", "m", "mountpoint", 1, 1)
        Dim argobj = parser.Parse(args)

        If argobj("MountPoint").params.Count <= 0 Then
            Console.WriteLine("No mount point specified")
            PrintUsage()
            Return
        End If

        If Not argobj("Device").found AndAlso Not argobj("Filename").found Then
            Console.WriteLine("Cannot neither device nor file")
            PrintUsage()
            Return
        End If
        If argobj("Device").found AndAlso argobj("Filename").found Then
            Console.WriteLine("Cannot both device and file")
            PrintUsage()
            Return
        End If

        Dim opt As New IFS1MountOptions

        opt.Check = argobj("Check").found
        opt.Repair = argobj("Repair").found
        opt.ReadOnlyMount = argobj("ReadOnly").found

        If argobj("Filename").found Then
            If argobj("Filename").params.Count <= 0 Then
                Console.WriteLine("No filename specified")
                PrintUsage()
                Return
            End If

            Dim fa = FileAccess.ReadWrite
            If argobj("ReadOnly").found Then
                fa = FileAccess.Read
            End If

            Console.WriteLine("Mount using Dokan driver...")

            Using fs As New FileStream(argobj("Filename").params(0), FileMode.Open, fa)
                Dim ifs As New IFS1(logger, fs, opt)
                'ifs.ReadOnlyMount = True
                Mount(ifs, argobj("MountPoint").params(0))
            End Using
        Else 'Device
            If argobj("Device").params.Count <= 0 Then
                Console.WriteLine("No device specified")
                PrintUsage()
                Return
            End If

            Console.WriteLine("Mount using Dokan driver...")

            Dim mode = Win32Native.GENERIC_READ Or Win32Native.GENERIC_WRITE
            If argobj("ReadOnly").found Then
                'mode = Win32Native.GENERIC_READ
            End If

            Dim drvs = My.Computer.FileSystem.Drives
            Dim drvinfo As DriveInfo = Nothing
            For Each drv In drvs
                If drv.Name(0) = argobj("Device").params(0)(0) Then 'AndAlso drv.DriveType <> DriveType.Fixed Then
                    drvinfo = drv
                End If
            Next

            If drvinfo Is Nothing Then
                Throw New Exception("Drive not found")
            End If

            Dim totalSize = drvinfo.TotalSize

            Using ds As New DeviceStream(argobj("Device").params(0)(0) + ":", mode)
                ds.SetLength(totalSize)
                Using bs As New BufferedStream(ds)
                    'IFS1.MakeFS(ds, totalSize, True)
                    Dim ifs As New IFS1(logger, bs, opt)
                    'ifs.ReadOnlyMount = True
                    Mount(ifs, argobj("MountPoint").params(0))
                End Using
            End Using
            'Throw New NotImplementedException()
        End If
    End Sub

    Public Sub MakeFS(args As String())

    End Sub

    Sub Main(args As String())
        If args.Length <= 0 Then
            PrintUsage()
            Return
        End If
        Dim cmd = args(0).ToLower()
        Dim values(args.Length - 2) As String
        Array.Copy(args, 1, values, 0, values.Length)
        Select Case cmd.ToLower
            Case "mount", "m"
                Mount(values)
            Case "mkfs", "makefs"
                MakeFS(values)
        End Select
        ''IFS1.MakeFS("test.ifs1", 1024 * 1024 * 1024 * 10L) '10GB
        ''Using fs As New FileStream("test.ifs1", FileMode.Open, FileAccess.ReadWrite)
        ''    Dim ifs As New IFS1(fs, True, True)
        ''    'ifs.ReadOnlyMount = True
        ''    mount(ifs, "S")
        ''End Using
        'Using fs As New FileStream("isystemx86.vhd", FileMode.Open, FileAccess.ReadWrite)
        '    Dim ifs As New IFS1(logger, fs, True, True)
        '    'ifs.ReadOnlyMount = True
        '    mount(ifs, "S")
        'End Using
        'Console.ReadKey()
    End Sub

    Sub Mount(ifs As IFS1, symbol As Char)
        Dim opt As New DokanOptions()
        opt.DebugMode = True
        opt.MountPoint = symbol + ":\"
        opt.VolumeLabel = "IFS1"
        opt.ThreadCount = 1
        opt.FileSystemName = "IFS1"

        Dim T As New Thread(Sub()
                                Dim drv As New IFS1Driver(ifs)
                                Dim status As Integer = DokanNet.DokanMain(opt, drv)
                                Select Case status
                                    Case DokanNet.DOKAN_DRIVE_LETTER_ERROR
                                        logger.WriteLine("DokanNet: Driver letter error")
                                    Case DokanNet.DOKAN_DRIVER_INSTALL_ERROR
                                        logger.WriteLine("DokanNet: Driver install error")
                                    Case DokanNet.DOKAN_MOUNT_ERROR
                                        logger.WriteLine("DokanNet: Mount error")
                                    Case DokanNet.DOKAN_START_ERROR
                                        logger.WriteLine("DokanNet: Start error")
                                    Case DokanNet.DOKAN_ERROR
                                        logger.WriteLine("DokanNet: Unknown error")
                                    Case DokanNet.DOKAN_SUCCESS
                                        'logger.WriteLine("Success")
                                    Case Else
                                        logger.WriteLine("DokanNet: Unknown status: {0}", status)
                                End Select
                            End Sub)
        T.Start()
        Console.Title = "Press any key to unmount."
        logger.WriteLine(Console.Title)
        Console.ReadKey()
        Dim unmount = DokanNet.DokanUnmount(symbol)
        logger.WriteLine("Unmount: {0}", IIf(unmount = 1, "done.", "Failed!"))
        T.Join()
        'Console.ReadKey()
        Environment.Exit(0)
    End Sub

End Module
