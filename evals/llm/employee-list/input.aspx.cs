using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.UI;

public partial class EmployeeList : Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        if (IsPostBack)
        {
            return;
        }

        string department = Request.QueryString["dept"] ?? "Sales";
        Session["LastDepartment"] = department;

        string connectionString = ConfigurationManager.ConnectionStrings["HrDb"].ConnectionString;
        using (var connection = new SqlConnection(connectionString))
        using (var command = new SqlCommand(
            "SELECT FirstName, LastName, Title FROM Employees WHERE Department = @dept ORDER BY LastName", connection))
        {
            command.Parameters.AddWithValue("@dept", department);
            var adapter = new SqlDataAdapter(command);
            var table = new DataTable();
            adapter.Fill(table);

            EmployeesGrid.DataSource = table;
            EmployeesGrid.DataBind();
            CountLabel.Text = table.Rows.Count + " employees in " + Server.HtmlEncode(department);
        }
    }
}
