<%@ Language="VBScript" %>
<!--#include virtual="/shared/db.asp"-->
<%
Dim term, category, sql, rs
term = Trim(Request("q"))
category = Request.QueryString("cat")

On Error Resume Next
sql = "SELECT ProductId, Name FROM Products WHERE Name LIKE '%" & term & "%'"
If IsNumeric(category) Then
    sql = sql & " AND CategoryId = " & CInt(category)
End If
sql = sql & " ORDER BY Name"
Set rs = conn.Execute(sql)
If Err.Number <> 0 Then
    Response.Write "Search is not available right now."
    Response.End
End If
On Error GoTo 0
%>
<h2>Results for "<%= term %>"</h2>
<p>Showing results for <% Response.Write Server.HTMLEncode(term) %></p>
<ul>
<% Do While Not rs.EOF %>
    <li><a href="product-details.asp?id=<%= rs("ProductId") %>"><%= Server.HTMLEncode(rs("Name")) %></a></li>
<%     rs.MoveNext
   Loop %>
</ul>
