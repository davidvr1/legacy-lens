<%@ Page Language="C#" AutoEventWireup="true" CodeFile="Profile.aspx.cs" Inherits="Profile" %>
<!DOCTYPE html>
<html>
<body>
    <h1>Profile of <%= Request.QueryString["user"] %></h1>
    <p>Encoded: <%: Request.QueryString["user"] %></p>
    <p>Also encoded: <%= Server.HtmlEncode(Request.QueryString["user"]) %></p>
    <p>Generated at <%= DateTime.Now %></p>
</body>
</html>
