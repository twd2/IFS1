Public Class IFS1ObjectContext

    Public id As String
    Public Path As String
    Public Block As IFS1Block
    Public Postion As UInt32
    Public access As IO.FileAccess,
           share As IO.FileShare,
           mode As IO.FileMode,
           options As IO.FileOptions

    Public Sub New()

    End Sub

    Public Function Type() As IFS1Block.BlockType
        Return Block.type
    End Function

End Class
