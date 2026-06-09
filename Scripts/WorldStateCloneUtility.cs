namespace MapEditorPrototype
{
    public static class WorldStateCloneUtility
    {
        public static WorldState Clone(WorldState source)
        {
            return source == null ? null : WorldStateDtoMapper.FromSaveData(WorldStateDtoMapper.ToSaveData(source));
        }
    }
}
