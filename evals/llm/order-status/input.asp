<%@ Language="VBScript" %>
<!--#include file="includes/db.inc"-->
<%
Dim conn, rs, orderId
orderId = Request.QueryString("orderId")

Set conn = Server.CreateObject("ADODB.Connection")
conn.Open DB_CONNECTION
Set rs = Server.CreateObject("ADODB.Recordset")
rs.Open "SELECT ProductName, Quantity, Status FROM OrderLines WHERE OrderId = " & orderId, conn
%>
<html>
<body>
<h1>Order <%= orderId %></h1>
<table>
<tr><th>Product</th><th>Quantity</th><th>Status</th></tr>
<% Do While Not rs.EOF %>
<tr>
  <td><%= rs("ProductName") %></td>
  <td><%= rs("Quantity") %></td>
  <td><%= rs("Status") %></td>
</tr>
<%
    rs.MoveNext
Loop
rs.Close
conn.Close
%>
</table>
</body>
</html>
