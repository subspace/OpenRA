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
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using OpenRA.Support;
using OpenRA.Network.Server;

namespace OpenRA.Network.Client
{
	class NetworkConnection : EchoConnection, IDisposable
	{
		TcpClient socket;
		int clientId;
		ConnectionState connectionState = ConnectionState.Connecting;
		Thread t;

		public NetworkConnection( string host, int port )
		{
			t = new Thread( _ =>
			{
				try
				{
					socket = new TcpClient( host, port );
					socket.NoDelay = true;
					var reader = new BinaryReader( socket.GetStream() );
					var serverProtocol = reader.ReadInt32();

					if( ProtocolVersion.Version != serverProtocol )
						throw new InvalidOperationException(
							"Protocol version mismatch. Server={0} Client={1}"
								.F( serverProtocol, ProtocolVersion.Version ) );

					clientId = reader.ReadInt32();
					connectionState = ConnectionState.Connected;

					for( ; ; )
					{
						var len = reader.ReadInt32();
						var client = reader.ReadInt32();
						var buf = reader.ReadBytes( len );
						if( len == 0 )
							throw new NotImplementedException();
						lock( this )
							receivedPackets.Add( new ReceivedPacket { FromClient = client, Data = buf } );
					}
				}
				catch( SocketException )
				{
					connectionState = ConnectionState.NotConnected;
				}
				catch( IOException ) { socket.Close(); }
				catch( ThreadAbortException ) { socket.Close(); }
			}
			) { IsBackground = true };
			t.Start();
		}

		public override int LocalClientId { get { return clientId; } }
		public override ConnectionState ConnectionState { get { return connectionState; } }

		public override void Send( byte[] packet )
		{
			base.Send( packet );

			try
			{
				var ms = new MemoryStream();
				ms.Write( BitConverter.GetBytes( (int)packet.Length ) );
				ms.Write( packet );
				ms.WriteTo( socket.GetStream() );

			}
			catch( SocketException ) { /* drop this on the floor; we'll pick up the disconnect from the reader thread */ }
			catch( ObjectDisposedException ) { /* ditto */ }
		}

		bool disposed = false;

		public void Dispose()
		{
			if( disposed ) return;
			disposed = true;
			GC.SuppressFinalize( this );

			t.Abort();
			if( socket != null )
				socket.Client.Close();
			using( new PerfSample( "Thread.Join" ) )
				t.Join();
		}

		~NetworkConnection() { Dispose(); }
	}
}
