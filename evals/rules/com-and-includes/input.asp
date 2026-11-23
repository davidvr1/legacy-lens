<%@ Language="VBScript" %>
<!--#include file="header.inc"-->
<!--#include virtual="/lib/utils.asp"-->
<%
Dim conn, fso, mail, xml
Set conn = Server.CreateObject("ADODB.Connection")
Set fso = Server.CreateObject("Scripting.FileSystemObject")
Set mail = Server.CreateObject("CDO.Message")
' Set xml = Server.CreateObject("MSXML2.DOMDocument")
Response.Write "Server.CreateObject is documented in the help page."
%>
<p>Include files are listed at the bottom.</p>
<!--#include file="footer.inc"-->
