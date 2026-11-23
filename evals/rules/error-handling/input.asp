<%@ Language="VBScript" %>
<%
Sub SaveOrder(orderId)
    On Error Resume Next
    conn.Execute "UPDATE Orders SET Saved = 1 WHERE Id = ?"
    On Error GoTo 0
End Sub

Sub DeleteOrder(orderId)
    On Error Resume Next
    conn.Execute "DELETE FROM Orders WHERE Id = ?"
    If Err.Number <> 0 Then
        Response.Write "Delete failed."
        Err.Clear
    End If
    On Error GoTo 0
End Sub
%>
