<%@ Language="VBScript" %>
<%
Dim q, safeQ
q = Request.QueryString("q")
safeQ = Server.HTMLEncode(Request.QueryString("q"))

Response.Write "<p>You searched for: " & Request("q") & "</p>"
Response.Write "<p>Encoded: " & Server.HTMLEncode(Request("q")) & "</p>"
Response.Write "<p>Static text</p>"
Response.Write q
Response.Write safeQ
%>
<html>
<body>
<h2><%= Request.QueryString("title") %></h2>
<h3><%= Server.HTMLEncode(q) %></h3>
<p><%= Now() %></p>
<p><%= Session("UserName") %></p>
</body>
</html>
