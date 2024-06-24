using System;
using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CsvHelper;
using CsvHelper.Configuration;

namespace QueryCSV
{
    public partial class Form1 : Form
    {
        private SQLiteConnection connection;
        private DataTable dataTable;

        public Form1()
        {
            InitializeComponent();
            InitializeCustomComponents();
            InitializeDatabase();
        }

        private void InitializeCustomComponents()
        {
            cmbDelimiter.Items.Add("Comma");
            cmbDelimiter.Items.Add("Pipe");
            cmbDelimiter.SelectedIndex = 0;

            //openToolStripMenuItem.Click += openToolStripMenuItem_Click;
            //btnLoad.Click += btnLoad_Click;
            //btnExecute.Click += btnExecute_Click;

            backgroundWorker.DoWork += backgroundWorker_DoWork;
            backgroundWorker.ProgressChanged += backgroundWorker_ProgressChanged;
            backgroundWorker.RunWorkerCompleted += backgroundWorker_RunWorkerCompleted;
        }

        private void InitializeDatabase()
        {
            connection = new SQLiteConnection("Data Source=:memory:;Version=3;New=True;");
            connection.Open();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                txtFilePath.Text = openFileDialog.FileName;
            }
        }

        private void btnLoad_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtFilePath.Text) || !File.Exists(txtFilePath.Text))
            {
                MessageBox.Show("Please select a valid CSV file.");
                return;
            }

            if (!backgroundWorker.IsBusy)
            {
                progressBar.Value = 0;
                lblProgress.Text = "0%";
                backgroundWorker.RunWorkerAsync(new WorkerArgs
                {
                    FilePath = txtFilePath.Text,
                    Delimiter = cmbDelimiter.SelectedItem.ToString() == "Comma" ? ',' : '|',
                    HasHeaders = chkHeaders.Checked,
                    RowsToLoad = (int)numRows.Value
                });
            }
        }

        private void LoadCSVIntoSQLite(BackgroundWorker worker, DoWorkEventArgs e)
        {
            WorkerArgs args = (WorkerArgs)e.Argument;

            try
            {
                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    Delimiter = args.Delimiter.ToString(),
                    HasHeaderRecord = args.HasHeaders,
                    BadDataFound = null
                };

                using (var reader = new StreamReader(args.FilePath))
                using (var csv = new CsvReader(reader, config))
                {
                    int fieldCount;
                    if (args.HasHeaders)
                    {
                        csv.Read();
                        csv.ReadHeader();
                        fieldCount = csv.HeaderRecord.Length;

                        string createTableQuery = "CREATE TABLE IF NOT EXISTS CsvData (" +
                                                  string.Join(",", csv.HeaderRecord.Select(h => $"[{h}] TEXT")) + ")";
                        MessageBox.Show(createTableQuery);
                        using (var cmd = new SQLiteCommand(createTableQuery, connection))
                        {
                            cmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        csv.Read();
                        fieldCount = csv.Context.Parser.Record.Length;

                        string createTableQuery = "CREATE TABLE IF NOT EXISTS CsvData (" +
                                                  string.Join(",", Enumerable.Range(0, fieldCount).Select(i => $"Column{i + 1} TEXT")) + ")";
                        MessageBox.Show(createTableQuery);
                        using (var cmd = new SQLiteCommand(createTableQuery, connection))
                        {
                            cmd.ExecuteNonQuery();
                        }
                    }

                    using (var transaction = connection.BeginTransaction())
                    {
                        string insertQuery = "INSERT INTO CsvData VALUES (" +
                                             string.Join(",", Enumerable.Range(0, fieldCount).Select(_ => "?")) + ")";
                        using (var cmd = new SQLiteCommand(insertQuery, connection))
                        {
                            int totalRows = File.ReadLines(args.FilePath).Count();
                            int currentRow = 0;

                            while (csv.Read())
                            {
                                cmd.Parameters.Clear();
                                for (int i = 0; i < fieldCount; i++)
                                {
                                    var field = csv.GetField(i);
                                    cmd.Parameters.AddWithValue(null, field);
                                }
                                cmd.ExecuteNonQuery();

                                currentRow++;
                                int progressPercentage = (int)((float)currentRow / totalRows * 100);
                                worker.ReportProgress(progressPercentage);

                                if (args.RowsToLoad > 0 && --args.RowsToLoad == 0)
                                    break;
                            }
                        }
                        transaction.Commit();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading CSV into SQLite: " + ex.Message);
            }
        }


        private void backgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            LoadCSVIntoSQLite(sender as BackgroundWorker, e);
        }


        private void backgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => backgroundWorker_ProgressChanged(sender, e)));
            }
            else
            {
                progressBar.Value = e.ProgressPercentage;
                lblProgress.Text = e.ProgressPercentage.ToString() + "%";
            }
        }


        private void backgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => backgroundWorker_RunWorkerCompleted(sender, e)));
            }
            else
            {
                LoadDataGridView();
                progressBar.Value = 100;
                lblProgress.Text = "100%";
                //MessageBox.Show("CSV loading completed!");
                txtSQLQuery.Text = "Select * from CsvData";
            }
        }


        private void LoadDataGridView()
        {
            using (var cmd = new SQLiteCommand("SELECT name FROM sqlite_master WHERE type='table' AND name='CsvData';", connection))
            {
                var tableExists = cmd.ExecuteScalar();
                if (tableExists == null)
                {
                    MessageBox.Show("Table 'CsvData' does not exist.");
                    return;
                }
            }

            using (var adapter = new SQLiteDataAdapter("SELECT * FROM CsvData", connection))
            {
                dataTable = new DataTable();
                adapter.Fill(dataTable);
                dataGridView.DataSource = null; // Clear existing data source
                dataGridView.Columns.Clear(); // Clear existing columns

                if (chkHeaders.Checked)
                {
                    dataGridView.DataSource = dataTable;
                }
                else
                {
                    // Create columns with default names
                    for (int i = 0; i < dataTable.Columns.Count; i++)
                    {
                        dataGridView.Columns.Add($"Column{i + 1}", $"Column{i + 1}");
                    }

                    // Add rows to the DataGridView
                    foreach (DataRow row in dataTable.Rows)
                    {
                        dataGridView.Rows.Add(row.ItemArray);
                    }
                }
            }
        }




        private void btnExecute_Click(object sender, EventArgs e)
        {
            ExecuteQuery();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void txtSQLQuery_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F5)
            {
                ExecuteQuery();
                e.Handled = true;
                e.SuppressKeyPress = true; // Prevent default behavior of F5 key
            }
        }

        private void ExecuteQuery()
        {
            if (dataTable == null)
            {
                MessageBox.Show("Please load the CSV file first.");
                return;
            }

            string query = txtSQLQuery.Text;
            using (SQLiteDataAdapter adapter = new SQLiteDataAdapter(query, connection))
            {
                DataTable filteredTable = new DataTable();
                try
                {
                    adapter.Fill(filteredTable);
                    dataGridView.DataSource = filteredTable;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error executing query: " + ex.Message);
                }
            }
        }

    }
}
