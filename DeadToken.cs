namespace Deadheim
{
    /// <summary>
    /// Dead Token: moeda do Deadheim (moeda grossa de ouro com um valknut, clonada das moedas do
    /// jogo). Por enquanto e so o item: nenhuma construcao custa Dead Token.
    ///
    /// O 7.5.0 fazia dele o custo do portal, do spawner e da Ward de Territorio e convertia os
    /// tokens antigos; o 7.5.1 voltou atras. PortalToken, SpawnerToken e TerritoryToken seguem
    /// sendo o custo de cada construcao, como no 7.4.0.
    /// </summary>
    internal static class DeadToken
    {
        public const string Prefab = "DeadToken";
        public const string Nome = "Dead Token";
    }
}
