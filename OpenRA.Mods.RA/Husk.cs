﻿#region Copyright & License Information
/*
 * Copyright 2007-2010 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made 
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see LICENSE.
 */
#endregion

using System.Collections.Generic;
using OpenRA.Traits;

namespace OpenRA.Mods.RA
{
	class HuskInfo : ITraitInfo
	{
		public object Create( ActorInitializer init ) { return new Husk( init ); }
	}

	class Husk : IOccupySpace, IFacing
	{
		Actor self;
		[Sync]
		int2 location;
		
		[Sync]
		public int Facing { get; set; }
		public int ROT { get { return 0; } }
		public int InitialFacing { get { return 128; } }

		public Husk(ActorInitializer init)
		{
			this.self = init.self;
			this.location = init.Get<LocationInit,int2>();
			this.CenterLocation = Util.CenterOfCell( this.location );
			this.Facing = init.Contains<FacingInit>() ? init.Get<FacingInit,int>() : 128;
			self.World.WorldActor.Trait<UnitInfluence>().Add(self, this);
		}

		public int2 TopLeft { get { return location; } }

		public float2 CenterLocation { get; set; }

		public IEnumerable<int2> OccupiedCells() { yield return TopLeft; }
	}
}
