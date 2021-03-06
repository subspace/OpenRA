﻿#region Copyright & License Information
/*
 * Copyright 2007-2010 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made 
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see LICENSE.
 */
#endregion

using System.Windows.Forms;

namespace OpenRA.Editor
{
	public partial class NewMapDialog : Form
	{
		public NewMapDialog()
		{
			InitializeComponent();
		}

		private void SelectText(object sender, System.EventArgs e)
		{
			(sender as NumericUpDown).Select(0, (sender as NumericUpDown).ToString().Length);
		}
	}
}
