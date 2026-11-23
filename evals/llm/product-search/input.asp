<%@ Language="VBScript" %>
<!--#include file="header.inc"-->
<%
Dim keyword, conn, rs, sql

keyword = Request.Form("keyword")
If keyword = "" Then
    keyword = Request.Cookies("lastSearch")
End If
Response.Cookies("lastSearch") = keyword
Response.Cookies("lastSearch").Expires = Date() + 30

Set conn = Server.CreateObject("ADODB.Connection")
conn.Open Application("ShopDb")
Set rs = Server.CreateObject("ADODB.Recordset")
sql = "SELECT Name, Price FROM Products WHERE Name LIKE '%" & keyword & "%' ORDER BY Name"
rs.Open sql, conn
%>
<form method="post" action="product-search.asp">
  <input type="text" name="keyword" value="<%= Server.HTMLEncode(keyword) %>">
  <input type="submit" value="Search">
</form>
<ul>
<% Do While Not rs.EOF %>
  <li><%= Server.HTMLEncode(rs("Name")) %> - <%= FormatCurrency(rs("Price")) %></li>
<%
    rs.MoveNext
Loop
rs.Close
conn.Close
%>
</ul>
<!--#include file="footer.inc"-->
