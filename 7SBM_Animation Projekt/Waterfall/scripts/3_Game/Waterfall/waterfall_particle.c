modded class ParticleList
{
	static const int WATERFALL1 			= RegisterParticle( "WaterFall/data/" , "waterfall1");
	static const int WATERFALL2 			= RegisterParticle( "WaterFall/data/" , "waterfall2");
	static const int WATERFALL3 			= RegisterParticle( "WaterFall/data/" , "waterfall3");
	static const int WATERFALL4 			= RegisterParticle( "WaterFall/data/" , "waterfall4");
	static const int WATERFALL5 			= RegisterParticle( "WaterFall/data/" , "waterfall5");
	static const int WATERFALL_LARGE 		= RegisterParticle( "WaterFall/data/" , "waterfall_large");
};

class EffWaterFall1 : EffectParticle
{
	void EffWaterFall1()
 	{
		SetParticleID(ParticleList.WATERFALL1);
	}
};

class EffWaterFall2 : EffectParticle
{
	void EffWaterFall2()
 	{
		SetParticleID(ParticleList.WATERFALL2);
	}
};

class EffWaterFall3 : EffectParticle
{
	void EffWaterFall3()
 	{
		SetParticleID(ParticleList.WATERFALL3);
	}
};

class EffWaterFall4 : EffectParticle
{
	void EffWaterFall4()
 	{
		SetParticleID(ParticleList.WATERFALL4);
	}
};

class EffWaterFall5 : EffectParticle
{
	void EffWaterFall5()
 	{
		SetParticleID(ParticleList.WATERFALL5);
	}
};

class EffWaterFallLarge : EffectParticle
{
	void EffWaterFallLarge()
 	{
		SetParticleID(ParticleList.WATERFALL_LARGE);
	}
};