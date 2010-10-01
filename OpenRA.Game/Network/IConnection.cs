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

namespace OpenRA.Network
{
	interface IConnection
	{
		int LocalClientId { get; }
		ConnectionState ConnectionState { get; }
		void Send( byte[] packet );
		void Receive( Action<int, byte[]> packetFn );
	}

	enum ConnectionState
	{
		PreConnecting,
		NotConnected,
		Connecting,
		Connected,
	}
}
