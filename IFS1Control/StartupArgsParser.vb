Public Class StartupArgsParser

    Public shortNames As New Dictionary(Of Char, String)
    Public fullNames As New Dictionary(Of String, String)
    Public argObjs As New Dictionary(Of String, StartupArgsInfo)

    Public Sub AddArgument(name As String, shortName As String, fullName As String, minParamsCount As Integer, maxParamsCount As Integer)
        If shortName = "" AndAlso fullName = "" Then
            Throw New ArgumentNullException("shortName, fullName")
        End If
        If shortName <> "" Then
            Me.shortNames.Add(shortName.ToLower()(0), name)
        End If
        If fullName <> "" Then
            Me.fullNames.Add(fullName.ToLower(), name)
        End If
        argObjs.Add(name, New StartupArgsInfo(minParamsCount, maxParamsCount))
    End Sub

    Public Function Parse(args As String()) As Dictionary(Of String, StartupArgsInfo)
        If args.Length = 0 Then
            Throw New StartupArgsParseException("No argument")
        End If
        Dim i = 0
        Do While i < args.Length
            Dim arg = args(i)
            If arg.StartsWith("--") Then
                Dim lname = arg.Substring(2).ToLower()
                If Not fullNames.ContainsKey(lname) Then
                    'Throw New StartupArgsParseException("Unknown argument """ + lname + """")
                    i += 1
                    Continue Do
                End If

                Dim obj = argObjs(fullNames(lname))
                obj.found = True

                If i + obj.minParamsCount >= args.Length Then
                    Throw New StartupArgsParseException("Bad argument """ & arg & """")
                End If

                Dim readCount = 0
                i += 1
                Do While readCount < obj.maxParamsCount AndAlso (i < args.Length)
                    obj.params.Add(args(i))
                    readCount += 1
                    i += 1
                Loop
                i -= 1
            ElseIf arg.StartsWith("-") Then
                For j = 1 To arg.Length - 1
                    Dim sname = arg.ToLower()(j)
                    If Not shortNames.ContainsKey(sname) Then
                        'Throw New StartupArgsParseException("Unknown argument """ + sname + """")
                        Continue For
                    End If

                    Dim obj = argObjs(shortNames(sname))
                    obj.found = True

                    If i + obj.minParamsCount >= args.Length Then
                        Throw New StartupArgsParseException("Bad argument """ & arg & """")
                    End If

                    Dim readCount = 0
                    i += 1
                    Do While readCount < obj.maxParamsCount AndAlso (i < args.Length)
                        obj.params.Add(args(i))
                        readCount += 1
                        i += 1
                    Loop
                    i -= 1
                Next
            Else
                'Throw New StartupArgsParseException("Bad argument """ & arg & """")
            End If

            i += 1
        Loop
        Return argObjs
    End Function

End Class
