#region Copyright & License Information
/*
 * Copyright 2007-2010 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made 
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see LICENSE.
 */
#endregion

using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using OpenRA.FileFormats;
using OpenRA.GameRules;
using OpenRA.Graphics;
using OpenRA.Network;
using OpenRA.Network.Client;
using OpenRA.Network.Server;
using OpenRA.Support;
using OpenRA.Widgets;

using XRandom = OpenRA.Thirdparty.Random;

namespace OpenRA
{
	public static class Game
	{
		public static int CellSize { get { return modData.Manifest.TileSize; } }

		public static ModData modData;
		public static World world;
		public static Viewport viewport;
		public static Settings Settings;

		internal static OrderManager orderManager;

		public static XRandom CosmeticRandom = new XRandom();	// not synced

		public static Renderer Renderer;
		public static Session LobbyInfo = new Session();
		public static bool HasInputFocus = false;
		
		static void LoadMap(string uid)
		{
			var map = modData.PrepareMap(uid);
			
			viewport = new Viewport(new float2(Renderer.Resolution), map.TopLeft, map.BottomRight, Renderer);
			world = new World(modData.Manifest, map);
		}

		public static void MoveViewport(int2 loc)
		{
			viewport.Center(loc);
		}

		internal static string CurrentHost = "";
		internal static int CurrentPort = 0;

		internal static void JoinServer(string host, int port)
		{
			if (orderManager != null) orderManager.Dispose();

			CurrentHost = host;
			CurrentPort = port;

			lastConnectionState = ConnectionState.PreConnecting;
			ConnectionStateChanged();

			orderManager = new OrderManager(new NetworkConnection(host, port), ChooseReplayFilename());
		}

		static string ChooseReplayFilename()
		{
			return DateTime.UtcNow.ToString("OpenRA-yyyy-MM-ddThhmmssZ.rep");
		}

		static void JoinLocal()
		{
			lastConnectionState = ConnectionState.PreConnecting;
			ConnectionStateChanged();

			if (orderManager != null) orderManager.Dispose();
			orderManager = new OrderManager(new EchoConnection());
		}

		static int lastTime = Environment.TickCount;

		static void ResetTimer()
		{
			lastTime = Environment.TickCount;
		}

		internal static int RenderFrame = 0;
		internal static int LocalTick = 0;
		const int NetTickScale = 3;		// 120ms net tick for 40ms local tick

		public static event Action ConnectionStateChanged = () => { };
		static ConnectionState lastConnectionState = ConnectionState.PreConnecting;
		public static int LocalClientId { get { return orderManager.Connection.LocalClientId; } }

		static void Tick( World world )
		{
			if (orderManager.Connection.ConnectionState != lastConnectionState)
			{
				lastConnectionState = orderManager.Connection.ConnectionState;
				ConnectionStateChanged();
			}

			int t = Environment.TickCount;
			int dt = t - lastTime;
			if (dt >= Settings.Game.Timestep)
			{
				using (new PerfSample("tick_time"))
				{
					lastTime += Settings.Game.Timestep;
					Widget.DoTick(world);
					Sound.Tick();
					orderManager.TickImmediate(world);

					var isNetTick = LocalTick % NetTickScale == 0;

					if (!isNetTick || orderManager.IsReadyForNextFrame)
					{
						++LocalTick;

						Log.Write("debug", "--Tick: {0} ({1})", LocalTick, isNetTick ? "net" : "local");

						if (isNetTick) orderManager.Tick(world);

						world.OrderGenerator.Tick(world);
						world.Selection.Tick(world);
						world.Tick();

						PerfHistory.Tick();
					}
					else
						if (orderManager.FrameNumber == 0)
							lastTime = Environment.TickCount;
				}
			}

			using (new PerfSample("render"))
			{
				++RenderFrame;
				viewport.DrawRegions(world);
				Sound.SetListenerPosition(viewport.Location + .5f * new float2(viewport.Width, viewport.Height));
			}

			PerfHistory.items["render"].Tick();
			PerfHistory.items["batches"].Tick();
			PerfHistory.items["text"].Tick();
			PerfHistory.items["cursor"].Tick();

			MasterServerQuery.Tick();
		}

		internal static event Action LobbyInfoChanged = () => { };

		internal static void SyncLobbyInfo( World world, string data)
		{
			LobbyInfo = Session.Deserialize(data);

			if( !world.GameHasStarted )
				world.SharedRandom = new XRandom( LobbyInfo.GlobalSettings.RandomSeed );

			if (orderManager.Connection.ConnectionState == ConnectionState.Connected)
				world.SetLocalPlayer(orderManager.Connection.LocalClientId);

			if (orderManager.FramesAhead != LobbyInfo.GlobalSettings.OrderLatency
				&& !orderManager.GameStarted)
			{
				orderManager.FramesAhead = LobbyInfo.GlobalSettings.OrderLatency;
				Debug("Order lag is now {0} frames.".F(LobbyInfo.GlobalSettings.OrderLatency));
			}

			LobbyInfoChanged();
		}

		public static void IssueOrder(Order o) { orderManager.IssueOrder(o); }	/* avoid exposing the OM to mod code */


		public static event Action AfterGameStart = () => {};
		public static event Action BeforeGameStart = () => {};
		internal static void StartGame(string map)
		{
			world = null;
			BeforeGameStart();
			LoadMap(map);
			if (orderManager.GameStarted) return;
			Widget.SelectedWidget = null;

			LocalTick = 0;

			orderManager.StartGame();
			viewport.RefreshPalette();
			AfterGameStart();
		}

		public static void DispatchMouseInput(MouseInputEvent ev, MouseEventArgs e, Modifiers modifierKeys)
		{
			var world = Game.world;
			if (world == null) return;

			Sync.CheckSyncUnchanged( world, () =>
			{
				var mi = new MouseInput
				{
					Button = (MouseButton)(int)e.Button,
					Event = ev,
					Location = new int2( e.Location ),
					Modifiers = modifierKeys,
				};
				Widget.HandleInput( world, mi );
			} );
		}

		public static bool IsHost
		{
			get { return orderManager.Connection.LocalClientId == 0; }
		}

		internal static Session.Client LocalClient
		{
			get { return LobbyInfo.Clients.FirstOrDefault(c => c.Index == orderManager.Connection.LocalClientId); }
		}

		public static void HandleKeyEvent(KeyInput e)
		{
			var world = Game.world;
			if( world == null ) return;

			Sync.CheckSyncUnchanged( world, () =>
			{
				Widget.HandleKeyPress( e );
			} );
		}

		static Modifiers modifiers;
		public static Modifiers GetModifierKeys() { return modifiers; }
		public static void HandleModifierKeys(Modifiers mods) {	modifiers = mods; }

		internal static void Initialize(Arguments args)
		{
			AppDomain.CurrentDomain.AssemblyResolve += FileSystem.ResolveAssembly;

			var defaultSupport = Environment.GetFolderPath(Environment.SpecialFolder.Personal)
												+ Path.DirectorySeparatorChar + "OpenRA";

			SupportDir = args.GetValue("SupportDir", defaultSupport);
			Settings = new Settings(SupportDir + "settings.yaml", args);

			Log.LogPath = SupportDir + "Logs" + Path.DirectorySeparatorChar;
			Log.AddChannel("perf", "perf.log");
			Log.AddChannel("debug", "debug.log");
			Log.AddChannel("sync", "syncreport.log");

			FileSystem.Mount("."); // Needed to access shaders
			Renderer.Initialize( Game.Settings.Graphics.Mode );
			Renderer.SheetSize = Settings.Game.SheetSize;
			Renderer = new Renderer();
			
			LobbyInfo.GlobalSettings.Mods = Settings.Game.Mods;
			modData = new ModData( LobbyInfo.GlobalSettings.Mods );
			
			Sound.Initialize();
			PerfHistory.items["render"].hasNormalTick = false;
			PerfHistory.items["batches"].hasNormalTick = false;
			PerfHistory.items["text"].hasNormalTick = false;
			PerfHistory.items["cursor"].hasNormalTick = false;

			
			JoinLocal();
			StartGame(modData.Manifest.ShellmapUid);

			Game.BeforeGameStart += () => Widget.OpenWindow("INGAME_ROOT");

			Game.ConnectionStateChanged += () =>
			{
				Widget.CloseWindow();
				switch( Game.orderManager.Connection.ConnectionState )
				{
					case ConnectionState.PreConnecting:
						Widget.OpenWindow("MAINMENU_BG");
						break;
					case ConnectionState.Connecting:
						Widget.OpenWindow("CONNECTING_BG");
						break;
					case ConnectionState.NotConnected:
						Widget.OpenWindow("CONNECTION_FAILED_BG");
						break;
					case ConnectionState.Connected:
						var lobby = Widget.OpenWindow("SERVER_LOBBY");
						lobby.GetWidget<ChatDisplayWidget>("CHAT_DISPLAY").ClearChat();
						lobby.GetWidget("CHANGEMAP_BUTTON").Visible = true;
						lobby.GetWidget("LOCKTEAMS_CHECKBOX").Visible = true;
						lobby.GetWidget("DISCONNECT_BUTTON").Visible = true;
						//r.GetWidget("INGAME_ROOT").GetWidget<ChatDisplayWidget>("CHAT_DISPLAY").ClearChat();	
						break;
				}
			};

			modData.WidgetLoader.LoadWidget( Widget.RootWidget, "PERF_BG" );
			Widget.OpenWindow("MAINMENU_BG");

			ResetTimer();
		}

		static bool quit;
		internal static void Run()
		{
			while (!quit)
			{
				Tick( world );
				Application.DoEvents();
			}
		}

		public static void Exit() { quit = true; }

		public static Action<Color,string,string> AddChatLine = (c,n,s) => {};

		public static void Debug(string s, params object[] args)
		{
			AddChatLine(Color.White, "Debug", String.Format(s,args)); 
		}

		public static void Disconnect()
		{
			orderManager.Dispose();
			var shellmap = modData.Manifest.ShellmapUid;
			LobbyInfo = new Session();
			LobbyInfo.GlobalSettings.Mods = Settings.Game.Mods;
			JoinLocal();
			StartGame(shellmap);

			Widget.CloseWindow();
			Widget.OpenWindow("MAINMENU_BG");
		}

		static string baseSupportDir = null;
		public static string SupportDir
		{
			set
			{
				var dir = value;

				// Expand paths relative to the personal directory
				if (dir.ElementAt(0) == '~')
					dir = Environment.GetFolderPath(Environment.SpecialFolder.Personal) + dir.Substring(1);

				if (!Directory.Exists(dir))
					Directory.CreateDirectory(dir);

				baseSupportDir = dir + Path.DirectorySeparatorChar;
			}
			get { return baseSupportDir; }
		}

		public static T CreateObject<T>( string name )
		{
			return modData.ObjectCreator.CreateObject<T>( name );
		}
	}
}
