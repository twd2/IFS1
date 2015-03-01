Public Class IFS1Transaction
    Implements IDisposable

    Public Name As String
    Private ifs As IFS1
    Private BlockChanges As New List(Of IFS1Block)
    Private committed As Boolean = False

    Public Sub New(name As String, ifs As IFS1)
        Debug.Print(name + " created")
        Me.Name = name
        Me.ifs = ifs
    End Sub

    Public Sub AddBlockChange(blk As IFS1Block)
        SyncLock Me
            If committed Then
                Throw New IFS1TransactionCommittedException()
            End If
            BlockChanges.Add(blk)
        End SyncLock
    End Sub

    Public Sub Commit()
        SyncLock Me
            Debug.Print(Name + " committed")
            If committed Then
                Throw New IFS1TransactionCommittedException()
            End If
            committed = True
            For Each blk In BlockChanges
                ifs.EnqueueBlockChange(blk)
            Next
        End SyncLock
    End Sub

    Public Sub Rollback()
        SyncLock Me
            Debug.Print(Name + " rollbacked")
            If committed Then
                Throw New IFS1TransactionCommittedException()
            End If
            BlockChanges.Clear()
        End SyncLock
    End Sub

#Region "IDisposable Support"
    Private disposedValue As Boolean ' 检测冗余的调用

    ' IDisposable
    Protected Overridable Sub Dispose(disposing As Boolean)
        If Not Me.disposedValue Then
            If disposing Then
                ' TODO:  释放托管状态(托管对象)。
                If Not committed Then
                    Rollback()
                End If
            End If

            ' TODO:  释放非托管资源(非托管对象)并重写下面的 Finalize()。
            ' TODO:  将大型字段设置为 null。
        End If
        Me.disposedValue = True
    End Sub

    ' TODO:  仅当上面的 Dispose(ByVal disposing As Boolean)具有释放非托管资源的代码时重写 Finalize()。
    'Protected Overrides Sub Finalize()
    '    ' 不要更改此代码。    请将清理代码放入上面的 Dispose(ByVal disposing As Boolean)中。
    '    Dispose(False)
    '    MyBase.Finalize()
    'End Sub

    ' Visual Basic 添加此代码是为了正确实现可处置模式。
    Public Sub Dispose() Implements IDisposable.Dispose
        ' 不要更改此代码。    请将清理代码放入上面的 Dispose (disposing As Boolean)中。
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub
#End Region

End Class
