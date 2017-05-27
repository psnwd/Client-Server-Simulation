﻿using System;
using System.Windows.Forms;

namespace Presentation.WinForms.ClientApp
{
    public partial class IntroForm : Form
    {
        public IntroForm() => InitializeComponent();

        private void OnLoadIntroForm(object sender, EventArgs e)
        {
            ConfigControlsProperties();
            RegisterEventHendlers();
        }

        private void ConfigControlsProperties()
        {
            pickupProtocolComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            pickupProtocolComboBox.SelectedIndex = 0;
        }

        private void RegisterEventHendlers()
        {
            runClientButton.Click += (sender, args) =>
            {
                this.Hide();

                string selectedValue = (string) pickupProtocolComboBox.Text;

                FlowClientForm flowClientForm = new FlowClientForm
                {
                    ClientType = selectedValue
                };

                flowClientForm.Closed += (o, eventArgs) => this.Close();

                flowClientForm.Show();
            };
        }
    }
}