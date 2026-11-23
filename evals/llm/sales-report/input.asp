<%@ Language="VBScript" %>
<%
Dim conn, rs, reportYear, fso, csv

If Session("Role") <> "Admin" Then
    Response.Redirect "login.asp"
End If

reportYear = CInt(Request.QueryString("year"))

Set conn = Server.CreateObject("ADODB.Connection")
conn.Open Application("SalesDb")
Set rs = conn.Execute("SELECT Region, SUM(Amount) AS Total FROM Sales WHERE YEAR(SaleDate) = " & reportYear & " GROUP BY Region")

Set fso = Server.CreateObject("Scripting.FileSystemObject")
Set csv = fso.CreateTextFile(Server.MapPath("reports/sales-" & reportYear & ".csv"), True)
csv.WriteLine "Region,Total"
Do While Not rs.EOF
    csv.WriteLine rs("Region") & "," & rs("Total")
    rs.MoveNext
Loop
csv.Close
conn.Close

Response.Write "Report for " & reportYear & " saved."
%>
