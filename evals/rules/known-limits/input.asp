<%@ Language="VBScript" %>
<%
' Cases the pattern-based rules are known to get wrong. The expected findings are the truth.
Dim raw, clean, sql, rs

' Request data passes through a second variable and a function before output: still XSS.
raw = Request.QueryString("q")
clean = Trim(raw)
Response.Write "<p>" & clean & "</p>"

' A numeric cast makes the value safe in SQL: no finding expected.
sql = "SELECT * FROM Orders WHERE Id = " & CLng(Request.QueryString("id"))
Set rs = conn.Execute(sql)
%>
