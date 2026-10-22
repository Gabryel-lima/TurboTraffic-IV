using System.Collections.Generic;

// Comparador de identidade para Vehicle/Ped em coleções (HashSet, Dictionary).
//
// SEM ISTO, TODA COLEÇÃO DE VEÍCULO OU PED ESTÁ QUEBRADA. Motivo: a classe
// GTA.base.HandleObject sobrescreve GetHashCode() (baseado no handle nativo) e
// implementa IEquatable<HandleObject>, mas NÃO sobrescreve Equals(object) e NÃO
// implementa IEquatable<Vehicle> nem IEquatable<Ped>. Como
// EqualityComparer<T>.Default só procura IEquatable<T> — com T exatamente
// Vehicle ou Ped —, ele cai no Equals(object) herdado de System.Object, que é
// comparação por REFERÊNCIA.
//
// E não dá para contar com a mesma instância para o mesmo carro: World.GetAllVehicles
// passa por um cache por handle da biblioteca (ContentCache — ver reverse/knowledge,
// achado F-006), mas outros caminhos (GetPedOnSeat, CurrentVehicle) e o despejo
// desse cache podem entregar wrappers diferentes para o mesmo handle. Um objeto
// diferente tem o mesmo hash mas é "diferente" para a coleção. O
// resultado era um Contains() que nunca acertava e um "restaurar quem não foi
// tocado neste tick" que desfazia, no mesmo frame, o que acabara de ser
// aplicado — foi isso que fazia a invencibilidade não pegar e o Modo Solto
// brigar consigo mesmo (limpar a tarefa e devolver a IA no mesmo frame).
//
// Aqui a comparação é delegada ao Equals(HandleObject) da própria biblioteca —
// o mesmo contrato de identidade que o operador == usa nas comparações diretas
// espalhadas pelo mod (v == playerVeh, p == Player.Character).
public sealed class HandleObjectComparer<T> : IEqualityComparer<T> where T : GTA.@base.HandleObject
{
    public bool Equals(T a, T b)
    {
        if (ReferenceEquals(a, b))
            return true;

        if (a == null || b == null)
            return false;

        // Resolve para Equals(HandleObject), não para Equals(object).
        return a.Equals(b);
    }

    public int GetHashCode(T obj)
    {
        // Já é derivado do handle nativo pela biblioteca.
        return obj == null ? 0 : obj.GetHashCode();
    }
}
