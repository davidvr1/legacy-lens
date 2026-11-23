<%@ Language="VBScript" %>
<%
If Session("UserId") = "" Then
    Response.Redirect "login.asp"
End If

Session("LastVisit") = Now()
Session("PageCount") = Session("PageCount") + 1
Session("Theme") = "dark"
Session("Lang") = "en"
Session("CartId") = 12
Session("Currency") = "EUR"
Session("Region") = "EU"
Session("Step") = 2
Session("Coupon") = ""
%>
