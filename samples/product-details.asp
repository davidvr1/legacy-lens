<%@ Language="VBScript" %>
<!--#include file="includes/header.asp"-->
<%
' Product details page: shows one product by id.
Dim conn, rs, sql, productId
productId = Request.QueryString("id")

Set conn = Server.CreateObject("ADODB.Connection")
conn.Open "Provider=SQLOLEDB;Data Source=SQL01;Initial Catalog=Shop;User ID=web;Password=Shop2003!;"

sql = "SELECT Name, Price, Description FROM Products WHERE ProductId = " & productId
Set rs = conn.Execute(sql)
%>
<h1><%= rs("Name") %></h1>
<p>Price: <%= FormatCurrency(rs("Price")) %></p>
<p><%= rs("Description") %></p>
<p>You came from: <%= Request.ServerVariables("HTTP_REFERER") %></p>
<a href="product-details.asp?id=<%= productId %>">Permalink</a>
<%
rs.Close
conn.Close
%>
<!--#include file="includes/footer.asp"-->
