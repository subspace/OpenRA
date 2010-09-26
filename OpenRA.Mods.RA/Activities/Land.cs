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
using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.RA.Activities
{
	public class Land : CancelableActivity
	{
		Target Target;

		public Land(Target t) { Target = t; }
		
		public override IActivity Tick(Actor self)
		{
			if (!Target.IsValid)
				Cancel(self);
			
			if (IsCanceled) return NextActivity;

			var d = Target.CenterLocation - self.CenterLocation;
			if (d.LengthSquared < 50)		/* close enough */
				return NextActivity;

			var aircraft = self.Trait<Aircraft>();

			if (aircraft.Altitude > 0)
				--aircraft.Altitude;

			var desiredFacing = Util.GetFacing(d, aircraft.Facing);
			aircraft.Facing = Util.TickFacing(aircraft.Facing, desiredFacing, aircraft.ROT);
			var speed = .2f * aircraft.MovementSpeedForCell(self);
			var angle = aircraft.Facing / 128f * Math.PI;

			aircraft.PxPosition += (speed * -float2.FromAngle((float)angle)).ToInt2();

			return this;
		}
	}
}
