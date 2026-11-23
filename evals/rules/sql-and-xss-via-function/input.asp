<%@ Language="VBScript" %>
<%
Dim name, city, sql, rs
name = Trim(Request.Form("name"))
city = UCase(Request.QueryString("city"))

sql = "SELECT * FROM Customers WHERE Name = '" & name & "' AND City = '" & city & "'"
Set rs = conn.Execute(sql)

Response.Write "<h1>Results for " & name & "</h1>"
%>
<p>City: <%= city %></p>
