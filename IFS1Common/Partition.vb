Imports System.Runtime.InteropServices

<StructLayout(LayoutKind.Sequential, Pack:=1)>
Public Class Partition

    Public BootInd As Byte = 0
    Public StartHead As Byte
    Public StartSecCyl1 As Byte
    Public StartSecCyl2 As Byte
    Public Type As Byte = &H83
    Public EndHead As Byte
    Public EndSecCyl1 As Byte
    Public EndSecCyl2 As Byte
    Public SectorsPreceding As UInteger
    Public TotalSectors As UInteger
    Public Const SectorMask = &H3F
    Public Const CylinderHighMask = &HC0


    Public Overloads Function ToString() As String
        Return String.Format("Type: {0}, Offset: {1}, Size: {2}", Type, CULng(SectorsPreceding) * 512, CULng(TotalSectors) * 512)
    End Function

End Class
