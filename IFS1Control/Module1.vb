Imports IFS1Common
Imports System.IO
Imports Dokan
Imports System.Threading

Module Module1

    Sub Main()
        'IFS1.MakeFS("test.ifs1", 1024 * 1024 * 1024 * 10L) '10GB
        Using fs As New FileStream("test.ifs1", FileMode.Open, FileAccess.ReadWrite)
            Dim ifs As New IFS1(fs, True, True)
            'ifs.ReadOnlyMount = True
            mount(ifs, "S")
        End Using
        'Using fs As New FileStream("isystemx86.vhd", FileMode.Open, FileAccess.ReadWrite)
        '    Dim ifs As New IFS1(fs, True, True)
        '    'ifs.ReadOnlyMount = True
        '    mount(ifs, "S")
        'End Using
        Console.ReadKey()
    End Sub

    Sub mount(ifs As IFS1, symbol As Char)
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
                                        Console.WriteLine("Driver letter error")
                                    Case DokanNet.DOKAN_DRIVER_INSTALL_ERROR
                                        Console.WriteLine("Driver install error")
                                    Case DokanNet.DOKAN_MOUNT_ERROR
                                        Console.WriteLine("Mount error")
                                    Case DokanNet.DOKAN_START_ERROR
                                        Console.WriteLine("Start error")
                                    Case DokanNet.DOKAN_ERROR
                                        Console.WriteLine("Unknown error")
                                    Case DokanNet.DOKAN_SUCCESS
                                        Console.WriteLine("Success")
                                    Case Else
                                        Console.WriteLine("Unknown status: %d", status)
                                End Select
                            End Sub)
        T.Start()
        Console.ReadKey()
        Console.WriteLine(DokanNet.DokanUnmount(symbol))
        T.Join()
        'Console.ReadKey()
        Environment.Exit(0)
    End Sub

End Module
