﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osuTK;
using osuTK.Graphics;
using osu.Framework.Graphics;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace osu.Game.Storyboards
{
    public delegate CommandTimeline<T> CommandTimelineSelector<T>(CommandTimelineGroup commandTimelineGroup);

    public class CommandTimelineGroup
    {
        public CommandTimeline<float> X = new CommandTimeline<float>();
        public CommandTimeline<float> Y = new CommandTimeline<float>();
        public CommandTimeline<float> Scale = new CommandTimeline<float>();
        public CommandTimeline<Vector2> VectorScale = new CommandTimeline<Vector2>();
        public CommandTimeline<float> Rotation = new CommandTimeline<float>();
        public CommandTimeline<Color4> Colour = new CommandTimeline<Color4>();
        public CommandTimeline<float> Alpha = new CommandTimeline<float>();
        public CommandTimeline<BlendingParameters> BlendingParameters = new CommandTimeline<BlendingParameters>();
        public CommandTimeline<bool> FlipH = new CommandTimeline<bool>();
        public CommandTimeline<bool> FlipV = new CommandTimeline<bool>();

        private readonly ICommandTimeline[] timelines;

        public CommandTimelineGroup()
        {
            timelines = new ICommandTimeline[]
            {
                X,
                Y,
                Scale,
                VectorScale,
                Rotation,
                Colour,
                Alpha,
                BlendingParameters,
                FlipH,
                FlipV
            };
        }

        [JsonIgnore]
        public double CommandsStartTime => timelines.Min(static t => t.StartTime);

        [JsonIgnore]
        public double CommandsEndTime => timelines.Max(static t => t.EndTime);

        [JsonIgnore]
        public double CommandsDuration => CommandsEndTime - CommandsStartTime;

        [JsonIgnore]
        public virtual double StartTime => CommandsStartTime;

        [JsonIgnore]
        public virtual double EndTime => CommandsEndTime;

        [JsonIgnore]
        public bool HasCommands => timelines.Any(static t => t.HasCommands);

        public virtual IEnumerable<CommandTimeline<T>.TypedCommand> GetCommands<T>(CommandTimelineSelector<T> timelineSelector, double offset = 0)
        {
            if (offset != 0)
            {
                return timelineSelector(this).Commands.Select(command =>
                    new CommandTimeline<T>.TypedCommand
                    {
                        Easing = command.Easing,
                        StartTime = offset + command.StartTime,
                        EndTime = offset + command.EndTime,
                        StartValue = command.StartValue,
                        EndValue = command.EndValue,
                    });
            }

            return timelineSelector(this).Commands;
        }
    }
}
