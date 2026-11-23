<%@ Language="VBScript" %>
<%
Dim connStr, connStr2, apiKey, pwd, conn

' Hard-coded connection string with a password
connStr = "Provider=SQLOLEDB;Data Source=dbserver;Initial Catalog=Shop;User Id=shop;Password=Fake_Pass_123;"

' Connection string from configuration: safe
connStr2 = Application("ConnectionString")

' Hard-coded API key
Const PAYMENT_API_KEY = "fake-key-0000-1111-2222"

' Password from the user, compared with an empty string: safe
pwd = Request.Form("password")
If pwd = "" Then
    Response.Redirect "login.asp"
End If

' Commented out old string: ignored
' connStr = "Provider=SQLOLEDB;Data Source=old;User Id=sa;Password=OldFake_1;"

Set conn = Server.CreateObject("ADODB.Connection")
conn.Open connStr
%>
