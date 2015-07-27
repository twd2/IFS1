Imports IFS1Common

Public Class FileContext

    Public [When] As DateTime = DateTime.Now

    Public Filename As String

    Public Access As DokanNet.FileAccess,
        Share As IO.FileShare,
        Mode As IO.FileMode,
        Options As IO.FileOptions,
        Attributes As IO.FileAttributes

    Public Block As IFS1FileBlock

    Public Sub New()

    End Sub

End Class
