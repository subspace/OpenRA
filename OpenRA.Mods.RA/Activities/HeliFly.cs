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
using OpenRA.Traits;

namespace OpenRA.Mods.RA.Activities
{
	class HeliFly : CancelableActivity
	{
		public readonly int2 Dest;
		public HeliFly(int2 dest)
		{
			Dest = dest;
		}

		public override IActivity Tick(Actor self)
		{
			if (IsCanceled)	return NextActivity;

			return
				Util.SequenceActivities(
					new HeliFlyPart( self.Trait<Aircraft>().PxPosition, Dest ),
					NextActivity )
				.Tick( self );
		}

		public override IEnumerable<float2> GetCurrentPath()
		{
			yield return Dest;
		}

		class HeliFlyPart : CancelableActivity
		{
			int2 pxStart, pxEnd;
			int length;
			int offset;

			public HeliFlyPart( int2 pxStart, int2 pxEnd )
			{
				this.pxStart = pxStart;
				this.pxEnd = pxEnd;
				var d = (float2)(pxEnd - pxStart);
				this.length = (int)Math.Ceiling( d.Length );
			}

			public override IActivity Tick( Actor self )
			{
				if (IsCanceled)	return NextActivity;

				var info = self.Info.Traits.Get<HelicopterInfo>();
				var aircraft = self.Trait<Aircraft>();

				if (aircraft.Altitude != info.CruiseAltitude)
				{
					aircraft.Altitude += Math.Sign(info.CruiseAltitude - aircraft.Altitude);
					return this;
				}

				var speed = .2f * aircraft.MovementSpeedForCell(self);
				offset += (int)Math.Ceiling( speed );
				if( offset >= length )
				{
					aircraft.PxPosition = pxEnd;
					return NextActivity;
				}

				var desiredFacing = Util.GetFacing(pxEnd - pxStart, aircraft.Facing);
				aircraft.Facing = Util.TickFacing(aircraft.Facing, desiredFacing, 
					aircraft.ROT);

				aircraft.PxPosition = int2.Lerp( pxStart, pxEnd, offset, length );
				return this;
			}
		}
	}
}
