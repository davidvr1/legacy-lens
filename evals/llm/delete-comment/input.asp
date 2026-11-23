<%@ Language="VBScript" %>
<!--#include file="includes/auth.inc"-->
<!--#include file="includes/db.inc"-->
<%
Dim commentId, postId, cmd

commentId = Request.QueryString("commentId")
postId = Request.QueryString("postId")

If Session("UserId") = "" Then
    Response.Redirect "login.asp"
End If

Set cmd = Server.CreateObject("ADODB.Command")
cmd.ActiveConnection = DB_CONNECTION
cmd.CommandText = "DELETE FROM Comments WHERE CommentId = " & commentId & " AND AuthorId = " & Session("UserId")
cmd.Execute

Response.Redirect "post.asp?id=" & postId
%>
