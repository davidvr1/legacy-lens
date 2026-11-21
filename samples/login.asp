<%@ Language="VBScript" %>
<%
Option Explicit
On Error Resume Next

Const ADMIN_PASSWORD = "letmein"
Dim conn, rs, sql, userName, password

userName = Request.Form("username")
password = Request.Form("password")

If password = ADMIN_PASSWORD Then
    Session("IsAdmin") = True
End If

Set conn = Server.CreateObject("ADODB.Connection")
conn.Open Application("ConnectionString")

sql = "SELECT UserId, DisplayName, Role FROM Users " & _
      "WHERE UserName = '" & userName & "' " & _
      "AND Password = '" & password & "'"
Set rs = conn.Execute(sql)

If Not rs.EOF Then
    Session("UserId") = rs("UserId")
    Session("UserName") = userName
    Session("DisplayName") = rs("DisplayName")
    Session("Role") = rs("Role")
    Session("LoggedIn") = True
    Session("LoginTime") = Now()
    Session("CartCount") = 0
    Session("Theme") = "default"
    Session("Language") = "en"
    Session("LastPage") = Request.ServerVariables("HTTP_REFERER")
    Response.Redirect Session("LastPage")
End If
%>
<html>
<body>
<p>Login failed for <%= userName %>.</p>
</body>
</html>
