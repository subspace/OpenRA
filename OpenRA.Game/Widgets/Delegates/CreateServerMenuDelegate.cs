#region Copyright & License Information
/*
 * Copyright 2007-2010 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made 
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see LICENSE.
 */
#endregion

using System.Linq;
using System.Net;

namespace OpenRA.Widgets.Delegates
{
	public class CreateServerMenuDelegate : IWidgetDelegate
	{
		[ObjectCreator.UseCtor]
		public CreateServerMenuDelegate( [ObjectCreator.Param( "widget" )] Widget cs )
		{
			var settings = Game.Settings;

			cs.GetWidget("BUTTON_CANCEL").OnMouseUp = mi => {
				Widget.CloseWindow();
				return true;
			};
			
			cs.GetWidget("BUTTON_START").OnMouseUp = mi => {
				var map = Game.modData.AvailableMaps.FirstOrDefault(m => m.Value.Selectable).Key;
				
				settings.Server.Name = cs.GetWidget<TextFieldWidget>("GAME_TITLE").Text;
				settings.Server.ListenPort = int.Parse(cs.GetWidget<TextFieldWidget>("LISTEN_PORT").Text);
				settings.Server.ExternalPort = int.Parse(cs.GetWidget<TextFieldWidget>("EXTERNAL_PORT").Text);
				settings.Save();

				Network.Server.Server.ServerMain(Game.modData, settings, map);

				Game.JoinServer(IPAddress.Loopback.ToString(), settings.Server.ListenPort);
				return true;
			};
			
			cs.GetWidget<TextFieldWidget>("GAME_TITLE").Text = settings.Server.Name;
			cs.GetWidget<TextFieldWidget>("LISTEN_PORT").Text = settings.Server.ListenPort.ToString();
			cs.GetWidget<TextFieldWidget>("EXTERNAL_PORT").Text = settings.Server.ExternalPort.ToString();
			cs.GetWidget<CheckboxWidget>("CHECKBOX_ONLINE").Checked = () => settings.Server.AdvertiseOnline;
			cs.GetWidget("CHECKBOX_ONLINE").OnMouseDown = mi => {
				settings.Server.AdvertiseOnline ^= true;
				settings.Save();
				return true;	
			};
			cs.GetWidget<CheckboxWidget>("CHECKBOX_CHEATS").Checked = () => settings.Server.AllowCheats;
			cs.GetWidget<CheckboxWidget>("CHECKBOX_CHEATS").OnMouseDown = mi => {
				settings.Server.AllowCheats ^=true;
				settings.Save();
				return true;
			};
		}
	}
}
