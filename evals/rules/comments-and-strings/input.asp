<%@ Language="VBScript" %>
<html>
<body>
<p>Never write Response.Write Request("x") in your pages.</p>
<p>A connection string looks like: Password=example;</p>
<%
' sql = "SELECT * FROM Users WHERE Id = " & Request("id")
Rem Response.Write Request("name")
Response.Write "Tip: avoid ""SELECT * FROM T WHERE Id = "" & Request(""id"")"
Dim msg
msg = "On Error Resume Next hides errors"
Response.Write msg
%>
</body>
</html>
