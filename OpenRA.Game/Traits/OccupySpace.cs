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
using System.Text;
using OpenRA.GameRules;

namespace OpenRA.Traits
{
	public abstract class OccupySpace : IOccupySpace
	{
		readonly Actor self;
		[Sync]
		int2 location;
		readonly string footprint;

		public OccupySpace( ActorInitializer init ) : this( init, "x" ) { }

		public OccupySpace( ActorInitializer init, string footprint )
		{
			this.self = init.self;
			this.location = init.Get<LocationInit, int2>();
			this.footprint = footprint;
			init.world.WorldActor.Trait<LocationCache>().uim.Add( self, this );
		}

		public int2 TopLeft
		{
			get { return location; }
			protected set
			{
				var uim = self.World.WorldActor.Trait<LocationCache>().uim;
				uim.Remove( self, this );
				location = value;
				uim.Add( self, this );
			}
		}

		public IEnumerable<int2> OccupiedCells()
		{
			return Footprint.UnpathableTiles( footprint, TopLeft );
		}
	}
}
