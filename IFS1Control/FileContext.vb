Imports IFS1Common

Public Class FileContext

    Public [when] As DateTime = DateTime.Now

    Public filename As String

    Public access As DokanNet.FileAccess,
        share As IO.FileShare,
        mode As IO.FileMode,
        options As IO.FileOptions,
        attributes As IO.FileAttributes

    Public blk As IFS1FileBlock

    Public Sub New()

    End Sub

End Class
