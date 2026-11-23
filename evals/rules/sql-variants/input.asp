<%@ Language="VBScript" %>
<!--#include file="db.inc"-->
<%
Dim rs, sql, cat, cmd

' 1) Request(...) concatenated directly
Set rs = conn.Execute("SELECT * FROM Products WHERE Id = " & Request("id"))

' 2) QueryString in a multi-line statement
sql = "SELECT * FROM Orders " & _
      "WHERE Status = '" & Request.QueryString("status") & "'"
Set rs = conn.Execute(sql)

' 3) Form value through a variable
cat = Request.Form("category")
sql = "SELECT Name FROM Products WHERE Category = '" & cat & "'"
Set rs = conn.Execute(sql)

' 4) Only literals concatenated: safe
sql = "SELECT TOP 10 * FROM Products " & "ORDER BY Name"
Set rs = conn.Execute(sql)

' 5) Parameterized command: safe
Set cmd = Server.CreateObject("ADODB.Command")
cmd.ActiveConnection = conn
cmd.CommandText = "SELECT * FROM Products WHERE Category = ?"
cmd.Parameters.Append cmd.CreateParameter("cat", 200, 1, 50, cat)
Set rs = cmd.Execute
%>
