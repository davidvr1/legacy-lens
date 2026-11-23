<%
' Shared data access helpers
Dim conn
Set conn = Server.CreateObject("ADODB.Connection")
conn.Open "Driver={SQL Server};Server=db02;Database=Shop;Uid=app;Pwd=Fake_App_Pass9;"

Function GetCustomer(customerId)
    Dim sql
    sql = "SELECT * FROM Customers WHERE Id = " & customerId
    Set GetCustomer = conn.Execute(sql)
End Function

Function GetCountries()
    Set GetCountries = conn.Execute("SELECT Code, Name FROM Countries ORDER BY Name")
End Function
%>
