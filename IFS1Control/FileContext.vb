Public Class FileContext

    Public filename As String

    Public access As DokanNet.FileAccess,
        share As IO.FileShare,
        mode As IO.FileMode,
        options As IO.FileOptions,
        attributes As IO.FileAttributes

    Public Sub New()

    End Sub

End Class
