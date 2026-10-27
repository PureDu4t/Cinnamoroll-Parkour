# Shared music SoundBank lifetime

The persistent TtsLoadingMusic owns a reference to Main. Scene AkBank components, WwiseStartEvent, and optional SceneMusicPlayer banks must release only their own load references.

The Addressables implementation of AK.Wwise.Bank.Unload in Assets/Wwise/API/Runtime/WwiseTypes/WwiseTypes/AkWwiseBank.cs explicitly passes ignoreRefCount:false and removeFromBankDictionary:false. Without these arguments the Addressables manager force-unloads a shared bank, stopping music posted from the persistent emitter.

Preserve this integration adjustment when updating Wwise Integration. Engine shutdown can still unload all banks.
