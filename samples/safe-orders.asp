<%@ Language="VBScript" %>
<!--#include file="includes/config.asp"-->
<%
' Lists the orders of the signed-in customer.
' Uses a parameterized command and encodes all output.
Dim conn, cmd, rs, customerId
customerId = CLng(Session("CustomerId"))

On Error Resume Next
Set conn = Server.CreateObject("ADODB.Connection")
conn.Open GetConnectionString()
If Err.Number <> 0 Then
    Response.Write "The order list is not available right now."
    Response.End
End If
On Error GoTo 0

Set cmd = Server.CreateObject("ADODB.Command")
Set cmd.ActiveConnection = conn
cmd.CommandText = "SELECT OrderId, OrderDate, Total FROM Orders WHERE CustomerId = ?"
cmd.Parameters.Append cmd.CreateParameter("@CustomerId", 3, 1, , customerId)
Set rs = cmd.Execute

' Old version, kept for reference:
' Set rs = conn.Execute("SELECT * FROM Orders WHERE CustomerId = " & Request("id"))
%>
<table>
<% Do While Not rs.EOF %>
  <tr>
    <td><%= Server.HTMLEncode(rs("OrderId")) %></td>
    <td><%= Server.HTMLEncode(FormatDateTime(rs("OrderDate"), 2)) %></td>
    <td><%= Server.HTMLEncode(FormatCurrency(rs("Total"))) %></td>
  </tr>
<%   rs.MoveNext
   Loop %>
</table>
