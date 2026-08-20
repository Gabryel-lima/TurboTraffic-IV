public struct VehicleEntry
{
    public readonly string Category;
    public readonly string DisplayName;
    public readonly string ModelName;

    public VehicleEntry(string category, string displayName, string modelName)
    {
        Category = category;
        DisplayName = displayName;
        ModelName = modelName;
    }
}

// Nomes internos de modelo curados a partir de referências públicas da comunidade
// de mods do GTA IV/EFLC. Se algum item não spawnar em jogo, o nome do modelo
// precisa ser corrigido aqui.
public static class VehicleCatalog
{
    public static readonly VehicleEntry[] Vehicles =
    {
        new VehicleEntry("Esportivos", "Infernus", "INFERNUS"),
        new VehicleEntry("Esportivos", "Banshee", "BANSHEE"),
        new VehicleEntry("Esportivos", "Comet", "COMET"),
        new VehicleEntry("Esportivos", "Turismo", "TURISMO"),
        new VehicleEntry("Esportivos", "Super GT", "SUPERGT"),
        new VehicleEntry("Esportivos", "Coquette", "COQUETTE"),

        new VehicleEntry("Sedans/Compactos", "Sultan", "SULTAN"),
        new VehicleEntry("Sedans/Compactos", "Stratum", "STRATUM"),
        new VehicleEntry("Sedans/Compactos", "Premier", "PREMIER"),
        new VehicleEntry("Sedans/Compactos", "Feroci", "FEROCI"),
        new VehicleEntry("Sedans/Compactos", "Blista Compact", "BLISTA"),
        new VehicleEntry("Sedans/Compactos", "Futo", "FUTO"),

        new VehicleEntry("SUVs/Utilitários", "Cavalcade", "CAVALCADE"),
        new VehicleEntry("SUVs/Utilitários", "Landstalker", "LANDSTAL"),
        new VehicleEntry("SUVs/Utilitários", "Huntley Sport", "HUNTLEY"),
        new VehicleEntry("SUVs/Utilitários", "Patriot", "PATRIOT"),
        new VehicleEntry("SUVs/Utilitários", "Rancher", "RANCHER"),

        new VehicleEntry("Motos", "PCJ 600", "PCJ600"),
        new VehicleEntry("Motos", "NRG-900", "NRG900"),
        new VehicleEntry("Motos", "Faggio", "FAGGIO"),
        new VehicleEntry("Motos", "Sanchez", "SANCHEZ"),
        new VehicleEntry("Motos", "Zombie", "ZOMBIEA"),

        new VehicleEntry("Serviço/Emergência", "Polícia (Cruiser)", "POLICE"),
        new VehicleEntry("Serviço/Emergência", "FBI", "FBI"),
        new VehicleEntry("Serviço/Emergência", "SWAT", "POLICET"),
        new VehicleEntry("Serviço/Emergência", "Ambulância", "AMBULANCE"),
        new VehicleEntry("Serviço/Emergência", "Bombeiro", "FIRETRUK"),
        new VehicleEntry("Serviço/Emergência", "Táxi", "TAXI"),
        new VehicleEntry("Serviço/Emergência", "Ônibus", "BUS"),

        new VehicleEntry("Aeronaves", "Annihilator (Militar)", "ANNIHILATOR"),
        new VehicleEntry("Aeronaves", "Maverick", "MAVERICK"),
        new VehicleEntry("Aeronaves", "Polícia (Helicóptero)", "POLMAV"),

        new VehicleEntry("Aquáticos", "Jetmax", "JETMAX"),
        new VehicleEntry("Aquáticos", "Predator", "PREDATOR"),
        new VehicleEntry("Aquáticos", "Tropic", "TROPIC"),
        new VehicleEntry("Aquáticos", "Dinghy", "DINGHY"),
    };
}
