''' <summary>
''' IFS1内部操作
''' </summary>
''' <remarks></remarks>
Partial Public Class IFS1

    ''' <summary>
    ''' 请求分配Block
    ''' </summary>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Private Function AllocBlock() As UInt32
        If ReadOnlyMount Then
            Throw New IFS1NoPermissionException()
        End If
        For i = 0 To BlocksCache.Count - 1
            If BlocksCache(i).used = 0 Then
                Return BlocksCache(i).id
            End If
        Next
        Return INVALID_BLOCK_ID
    End Function

    ''' <summary>
    ''' 批量申请Block
    ''' </summary>
    ''' <param name="count">数量</param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Private Function AllocBlock(count As UInt32) As UInt32()
        If ReadOnlyMount Then
            Throw New IFS1NoPermissionException()
        End If

        Dim blocks(count - 1) As UInt32
        Dim index = 0

        For i = 0 To BlocksCache.Count - 1
            If BlocksCache(i).used = 0 Then
                blocks(index) = BlocksCache(i).id
                index += 1
                If index = count Then
                    Exit For
                End If
            End If
        Next
        Return blocks
    End Function

    ''' <summary>
    ''' 请求分配Block, 但是排除一些Block
    ''' </summary>
    ''' <param name="exceptid"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Private Function AllocBlock(exceptid As List(Of UInt32)) As UInt32
        If ReadOnlyMount Then
            Throw New IFS1NoPermissionException()
        End If
        For i = 0 To BlocksCache.Count - 1
            If BlocksCache(i).used = 0 AndAlso exceptid.IndexOf(i) < 0 Then
                Return BlocksCache(i).id
            End If
        Next
        Return INVALID_BLOCK_ID
    End Function

    ''' <summary>
    ''' 批量申请Block, 但是排除一些Block
    ''' </summary>
    ''' <param name="count">数量</param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Private Function AllocBlock(count As UInt32, exceptid As List(Of UInt32)) As UInt32()
        If ReadOnlyMount Then
            Throw New IFS1NoPermissionException()
        End If

        Dim blocks(count - 1) As UInt32
        Dim index = 0

        For i = 0 To BlocksCache.Count - 1
            If BlocksCache(i).used = 0 AndAlso exceptid.IndexOf(i) < 0 Then
                blocks(index) = BlocksCache(i).id
                index += 1
                If index = count Then
                    Exit For
                End If
            End If
        Next
        Return blocks
    End Function

    Private Function ReadSector() As Byte()
        Dim buff(512 - 1) As Byte
        BinaryHelper.SafeRead(_s, buff, 0, 512)
        Return buff
    End Function

End Class
