<%@ Language="VBScript" %>
<!--#include virtual="/shared/config.inc"-->
<%
Dim email, fullName, conn, mail

email = Trim(Request.Form("email"))
fullName = Trim(Request.Form("fullName"))

If email = "" Then
    Response.Write "<p>Please enter an email address.</p>"
    Response.End
End If

Set conn = Server.CreateObject("ADODB.Connection")
conn.Open Application("NewsletterDb")
conn.Execute "INSERT INTO Subscribers (Email, FullName, CreatedAt) VALUES ('" & email & "', '" & fullName & "', GETDATE())"
conn.Close

Set mail = Server.CreateObject("CDO.Message")
mail.From = NEWSLETTER_SENDER
mail.To = email
mail.Subject = "Welcome to our newsletter"
mail.TextBody = "Hello " & fullName & ", thank you for signing up."
mail.Send

Response.Redirect "thanks.asp"
%>
