#version 300 es
#extension GL_OES_sample_variables : enable
precision highp float;
precision highp int;
precision highp sampler2D;

#define PBR_UNLIT

#include "Shadows.glsl"
#include "AmbientOcclusion.glsl"
#include "ColorSpace.glsl"
#include "Pbr.glsl"
