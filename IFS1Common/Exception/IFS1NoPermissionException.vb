Public Class IFS1NoPermissionException
    Inherits IFS1Exception

    Public Sub New()
        MyBase.New()
    End Sub

    Public Sub New(msg As String)
        MyBase.New(msg)
    End Sub
End Class
