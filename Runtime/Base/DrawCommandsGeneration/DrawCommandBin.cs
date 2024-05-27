using Scellecs.Morpeh.Graphics.Utilities;

namespace Scellecs.Morpeh.Graphics
{
    internal struct DrawCommandBin
    {
        public const int MaxInstancesPerCommand = BrgHelpers.MAX_INSTANCES_PER_DRAW_COMMAND;
        public const int kNoSortingPosition = -1;

        public int NumInstances;
        public int InstanceOffset;
        public int DrawCommandOffset;
        public int PositionOffset;

        // Use a -1 value to signal "no sorting position" here. That way,
        // when the offset is rewritten from a placeholder to a real offset,
        // the semantics are still correct, because -1 is never a valid offset.
        public bool HasSortingPosition => PositionOffset != kNoSortingPosition;

        public int NumDrawCommands => HasSortingPosition ? NumDrawCommandsHasPositions : NumDrawCommandsNoPositions;
        public int NumDrawCommandsHasPositions => NumInstances;
        // Round up to always have enough commands
        public int NumDrawCommandsNoPositions =>
            (MaxInstancesPerCommand - 1 + NumInstances) /
            MaxInstancesPerCommand;
    }
}
