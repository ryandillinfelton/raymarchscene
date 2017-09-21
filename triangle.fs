#ifdef GL_ES
precision mediump float;
#endif

const int MAX_MARCHING_STEPS = 255;
const float MIN_DIST = 0.0;
const float MAX_DIST = 150.0;
const float EPSILON = 0.0001;

uniform vec2 u_resolution;
uniform vec2 u_mouse;
uniform float u_time;

const float PI = 3.14159;

struct light {
	vec3 pos;
    vec3 color;
	float intensity;
} ;

struct rayCast {
    vec3 direction;
    float magnitude;
    int hit;
	 vec3 hitPoint;
} ray;

float rand(vec2 co){
    return fract(sin(dot(co.xy ,vec2(12.9898,78.233))) * 43758.5453);
}

//just testing git

/*
	Checks if the point is inside or outside of
	a unit sphere centered at the origin
*/

/*
	is the pt inside or outside of any object in the scene
	currently only checking one sphere
*/

float sdSphere( vec3 p, float s )
{
  return length(p)-s;
}


float sdBox( vec3 p, vec3 b )
{
  vec3 d = abs(p) - b;
  return min(max(d.x,max(d.y,d.z)),0.0) + length(max(d,vec3(0.0)));
}

float smin( float a, float b, float k )
{
    float h = clamp( 0.5+0.5*(b-a)/k, 0.0, 1.0 );
    return mix( b, a, h ) - k*h*(1.0-h);
}

float smoothUnion( float d1, float d2 )
{
    return smin(d1,d2, 0.3);
}

float sceneSDF(vec3 pt) {

    float floor = sdBox(pt-vec3(0,-1.5,0), vec3(30,0.0,20.0));
    float sphere = sdSphere(pt-vec3(0.0,1.0,0.0), 1.0);

    float value = smoothUnion(floor, sphere);

    if(value == floor) {
        ray.hit = 0;
    } else if (value == sphere) {
        ray.hit = 1;
    }
	return value;// + sdSphere(pt, 1.0);//sdBox(pt, vec3(1.0,-1.0,1.0));// + sphereAtPos(pt, vec3(0.0,2.0,0.0));
}

/*
	key function of the raymarch
	find the shortest distance to the surface of an object in the scene
	eye - the origin of the ray
	direction - the normalized direction to move (direction ray is facing)
	start/end - starting point on the ray, and max distance to move
*/

float shortestDistanceToSurface(vec3 startPoint, vec3 direction, float start, float end) {
	float depth = start;
	float dist;
	for(int i = 0; i < MAX_MARCHING_STEPS; i++) {
		vec3 nextPt = startPoint + depth * direction;
		dist = sceneSDF(nextPt);
		if(dist < EPSILON)
			return depth;
		depth += dist;
		if(depth >= end)
			return end;
	}
	return end;
}


/*
	ray direction - find the normalized direction to march in from
		the eye to a single pixel on the screen.

	perameters:
	fieldOfView - vertical fov in degrees
	size - resolution of the output image
	fragCoord - the x/y coordinate of the pizel in the output
				(screen x,y not adjusted UV)
*/

vec3 rayDirection(float fieldOfView, vec2 size, vec2 fragCoord) {
	vec2 xy = fragCoord - size/2.0;
	float z = size.y/tan(radians(fieldOfView)/2.0);
	return normalize(vec3(xy,-z));
}

vec3 estimateNormal(vec3 pt) {
	return normalize(vec3(
		sceneSDF(vec3(pt.x+EPSILON, pt.yz)) - sceneSDF(vec3(pt.x-EPSILON,pt.yz)),
		sceneSDF(vec3(pt.x, pt.y+EPSILON, pt.z)) - sceneSDF(vec3(pt.x,pt.y-EPSILON,pt.z)),
		sceneSDF(vec3(pt.xy,pt.z+EPSILON)) - sceneSDF(vec3(pt.xy, pt.z - EPSILON))
	));
}

//lighting start
vec3 diffuseLighting(vec3 pt, light currentLight, vec3 normal) {
	vec3 lightDir = normalize(currentLight.pos-pt);
    float lDotn = dot(lightDir, normal);
    return (currentLight.color * max(lDotn,0.0) * currentLight.intensity);
}

vec3 specularLighting(vec3 pt, vec3 normal, vec3 eye, light currentLight) {
    float shinyness = 8.0;
    vec3 l = normalize(currentLight.pos - pt);
    vec3 r = normalize(reflect(-l, normal));
    vec3 v = normalize(eye - pt);
    float rdotV = max(dot(r,v), 0.0);
    return (currentLight.color * currentLight.intensity * pow(rdotV, shinyness));
}

vec3 floorCheckerboard(vec3 pt) {
    vec3 color = vec3(0.981,0.985,0.995);

    float tile = floor(pt.x-(2.0 *floor(pt.x/2.0))) - floor(pt.z-(2.0*floor(pt.z/2.0)));
    if (tile < 0.0) {
        tile = 1.0;
    }


    return color * (1.0 - tile);
}

bool shadow(vec3 pt, light currentLight) {

	float dist = shortestDistanceToSurface(pt, normalize(currentLight.pos - pt), MIN_DIST+ 0.1, length(currentLight.pos - pt));
	vec3 shadowColor = vec3(-0.1, -0.5, -0.9);

    bool hit;
    if (dist < abs(length(currentLight.pos - pt))) {
        hit = true;
    } else if (dist == abs(length(currentLight.pos - pt))) {
		hit = false;
	}

	return	hit;
}

vec3 lighting(vec3 pt, vec3 eye, light currentLight) {
    vec3 sphereColor = vec3(0.183,0.830,0.822);
    vec3 floorColor = floorCheckerboard(pt);
    vec3 objectColor;
    if (ray.hit == 0) {
        objectColor = floorColor;
    } else if (ray.hit == 1) {
        objectColor = sphereColor;
    }

    vec3 ambientLight = vec3(0.35,0.35,0.35);
    vec3 normal = estimateNormal(pt);

    vec3 diffuse = diffuseLighting(pt,currentLight, normal);
    vec3 specular = specularLighting(pt, normal, eye, currentLight);

    bool shadow = shadow(pt, currentLight);

    //vec3 ptColor = (ambientLight + (diffuse + specular * float(!shadow)) + (float(shadow) * vec3(-0.2, -0.8, -0.8))) * objectColor;
	vec3 ptColor = (ambientLight + diffuse + specular) * objectColor;


	if (shadow) {
		ptColor = (float(shadow) * vec3(-0.1, -0.1, -0.1) + ambientLight) * objectColor;
	}

    return ptColor;
}

//lighting end

//main begin
void main() {
	vec3 background = vec3(0.750,0.750,0.750);
	//vec3 sphereColor = vec3(0.2,0.95,0.3);
    ray.direction = rayDirection(45.0, u_resolution.xy, gl_FragCoord.xy);
    //vec3 eye = vec3(0,sin(u_time/4.0)*0.5, sin(u_time)*2.0 + 12.0);
    vec3 eye = vec3(0, 0, 10);


    ray.magnitude = shortestDistanceToSurface(eye, ray.direction,  MIN_DIST, MAX_DIST);

    float noHit = step(ray.magnitude, MAX_DIST - EPSILON);

    vec3 pt = eye + ray.magnitude * ray.direction;


    //start lighting
    light mainlight;
	mainlight.pos = vec3(0.0,5.0,5.0);
    mainlight.color = vec3(1.0,1.0,1.0);
    mainlight.intensity = 0.8;

    vec3 lightingColor = lighting(pt, eye, mainlight);

	//the next line finds a vector between the light and the point on the
	//sphere
	//the next line finds the relationship between the normal of the point
	//and the light direction
	//max keeps the value zero if it's negative
    vec3 color = (1.0 - noHit)*background + (noHit*lightingColor);

	//cell shading
	//color = floor(color * 8.0)/8.0;

    gl_FragColor = vec4(color,1.0);
}
