Imports System.IO

Public Class LoggerWrapper
    Inherits TextWriter

    Private _innerTW As TextWriter

    Public Sub New(tw As TextWriter)
        _innerTW = tw
    End Sub

    Public Overrides ReadOnly Property Encoding As Text.Encoding
        Get

        End Get
    End Property
End Class
